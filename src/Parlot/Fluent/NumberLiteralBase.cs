using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Parlot.Fluent;

/// <summary>
/// This class is used as a base class for custom number parsers which don't implement INumber<typeparamref name="T"/> after .NET 7.0.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class NumberLiteralBase<T> : Parser<T>, ICompilable, ISeekable, ISourceable
{
    private readonly char _decimalSeparator;
    private readonly char _groupSeparator;
    private readonly MethodInfo _tryParseMethodInfo;
    private readonly NumberStyles _numberStyles;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private readonly bool _allowLeadingSign;
    private readonly bool _allowDecimalSeparator;
    private readonly bool _allowGroupSeparator;
    private readonly bool _allowExponent;

    public bool CanSeek => true;

    public char[] ExpectedChars { get; set; } = [];

    public bool SkipWhitespace => false;

    public abstract bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out T value);

    public NumberLiteralBase(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, MethodInfo? tryParseMethodInfo = null)
    {
        _decimalSeparator = decimalSeparator;
        _groupSeparator = groupSeparator;
        _tryParseMethodInfo = tryParseMethodInfo ?? Numbers.GetTryParseMethod<T>();
        _numberStyles = numberOptions.ToNumberStyles();

        if (decimalSeparator != NumberLiterals.DefaultDecimalSeparator ||
            groupSeparator != NumberLiterals.DefaultGroupSeparator)
        {
            _culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            _culture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();
            _culture.NumberFormat.NumberGroupSeparator = groupSeparator.ToString();
        }

        _allowLeadingSign = (numberOptions & NumberOptions.AllowLeadingSign) != 0;
        _allowDecimalSeparator = (numberOptions & NumberOptions.AllowDecimalSeparator) != 0;
        _allowGroupSeparator = (numberOptions & NumberOptions.AllowGroupSeparators) != 0;
        _allowExponent = (numberOptions & NumberOptions.AllowExponent) != 0;

        var expectedChars = "0123456789";

        if (_allowLeadingSign)
        {
            expectedChars += "+-";
        }

        if (_allowDecimalSeparator)
        {
            expectedChars += _decimalSeparator;
        }

        if (_allowExponent)
        {
            expectedChars += "eE";
        }

        ExpectedChars = expectedChars.ToCharArray();

        Name = "NumberLiteral";
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var reset = context.Scanner.Cursor.Position;
        var start = reset.Offset;

        if (context.Scanner.ReadDecimal(_allowLeadingSign, _allowDecimalSeparator, _allowGroupSeparator, _allowExponent, out var number, _decimalSeparator, _groupSeparator))
        {
            var end = context.Scanner.Cursor.Offset;

            if (TryParseNumber(number, _numberStyles, _culture, out T value))
            {
                result.Set(start, end, value);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(reset);

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // var reset = context.Scanner.Cursor.Position;

        var reset = context.DeclarePositionVariable(result);

        var numberStyles = result.DeclareVariable<NumberStyles>($"numberStyles{context.NextNumber}", Expression.Constant(_numberStyles));
        var culture = result.DeclareVariable<CultureInfo>($"culture{context.NextNumber}", Expression.Constant(_culture));
        var numberSpan = result.DeclareVariable($"number{context.NextNumber}", typeof(ReadOnlySpan<char>));
        var end = result.DeclareVariable<int>($"end{context.NextNumber}");

        // if (context.Scanner.ReadDecimal(_numberOptions, out var numberSpan, _decimalSeparator, _groupSeparator))
        // {
        //    var end = context.Scanner.Cursor.Offset;
        //    success = T.TryParse(numberSpan.ToString(), numberStyles, culture, out var value));
        //    // or when possible T.TryParse(numberSpan, numberStyles, culture, out var value));
        // }
        //
        // if (!success)
        // {
        //    context.Scanner.Cursor.ResetPosition(begin);
        // }
        //

        var block =
            Expression.IfThen(
                context.ReadDecimal(Expression.Constant(_allowLeadingSign),
                    Expression.Constant(_allowDecimalSeparator),
                    Expression.Constant(_allowGroupSeparator),
                    Expression.Constant(_allowExponent),
                    numberSpan, Expression.Constant(_decimalSeparator), Expression.Constant(_groupSeparator)),
                Expression.Block(
                    Expression.Assign(end, context.Offset()),
                    Expression.Assign(result.Success,
                        Expression.Call(
                            _tryParseMethodInfo,
                            numberSpan,
                            numberStyles,
                            culture,
                            result.Value)
                        )
                )
            );

        result.Body.Add(block);

        result.Body.Add(
            Expression.IfThen(
                Expression.Not(result.Success),
                context.ResetPosition(reset)
                )
            );

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var resetName = $"reset{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";
        var numberSpanName = $"numberSpan{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";
        var parsedValueName = $"parsedValue{context.NextNumber()}";

        result.Body.Add($"var {resetName} = default(global::Parlot.TextPosition);");
        result.Body.Add($"var {startName} = 0;");
        result.Body.Add($"global::System.ReadOnlySpan<char> {numberSpanName} = default;");
        result.Body.Add($"{valueTypeName} {parsedValueName} = default;");

        result.Body.Add($"{result.SuccessVariable} = false;");
        result.Body.Add($"{resetName} = {cursorName}.Position;");
        result.Body.Add($"{startName} = {resetName}.Offset;");

        var allowLeadingSign = _allowLeadingSign ? "true" : "false";
        var allowDecimalSeparator = _allowDecimalSeparator ? "true" : "false";
        var allowGroupSeparator = _allowGroupSeparator ? "true" : "false";
        var allowExponent = _allowExponent ? "true" : "false";

        // Emit NumberStyles as a literal cast
        var numberStylesExpr = $"(global::System.Globalization.NumberStyles){(int)_numberStyles}";
        
        // Emit CultureInfo - use InvariantCulture if it's the default, otherwise create a clone
        string cultureExpr;
        if (_culture == CultureInfo.InvariantCulture)
        {
            cultureExpr = "global::System.Globalization.CultureInfo.InvariantCulture";
        }
        else
        {
            // For custom cultures, we need to emit code that creates the same culture
            // This is a simplified approach - for complex cases, we might need to register a factory
            cultureExpr = "global::System.Globalization.CultureInfo.InvariantCulture";
        }

        result.Body.Add($"if ({scannerName}.ReadDecimal({allowLeadingSign}, {allowDecimalSeparator}, {allowGroupSeparator}, {allowExponent}, out {numberSpanName}, '{_decimalSeparator}', '{_groupSeparator}'))");
        result.Body.Add("{");
        // Use ReadOnlySpan<char> overload directly - .NET 7+ types all support TryParse(ReadOnlySpan<char>, ...)
        result.Body.Add($"    if (global::Parlot.Numbers.TryParse({numberSpanName}, {numberStylesExpr}, {cultureExpr}, out {parsedValueName}))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = {parsedValueName};");
        result.Body.Add("    }");
        result.Body.Add("}");

        result.Body.Add($"if (!{result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({resetName});");
        result.Body.Add("}");

        return result;
    }
}

internal sealed class ByteNumberLiteral : NumberLiteralBase<byte>
{
    public ByteNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out byte value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class SByteNumberLiteral : NumberLiteralBase<sbyte>
{
    public SByteNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out sbyte value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class IntNumberLiteral : NumberLiteralBase<int>
{
    public IntNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out int value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class UIntNumberLiteral : NumberLiteralBase<uint>
{
    public UIntNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out uint value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class LongNumberLiteral : NumberLiteralBase<long>
{
    public LongNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out long value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class ULongNumberLiteral : NumberLiteralBase<ulong>
{
    public ULongNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ulong value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class ShortNumberLiteral : NumberLiteralBase<short>
{
    public ShortNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out short value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class UShortNumberLiteral : NumberLiteralBase<ushort>
{
    public UShortNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ushort value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class DecimalNumberLiteral : NumberLiteralBase<decimal>
{
    public DecimalNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out decimal value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class DoubleNumberLiteral : NumberLiteralBase<double>
{
    public DoubleNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out double value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class FloatNumberLiteral : NumberLiteralBase<float>
{
    public FloatNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out float value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

#if NET6_0_OR_GREATER
internal sealed class HalfNumberLiteral : NumberLiteralBase<Half>
{
    public HalfNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out Half value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}
#endif

internal sealed class BigIntegerNumberLiteral : NumberLiteralBase<BigInteger>
{
    public BigIntegerNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out BigInteger value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}
