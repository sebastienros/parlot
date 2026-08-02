using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace Parlot.Fluent;

/// <summary>
/// This class is used as a base class for custom number parsers which don't implement INumber<typeparamref name="T"/> after .NET 7.0.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class NumberLiteralBase<T> : Parser<T>, ISeekable, ISourceable
{
    private readonly char _decimalSeparator;
    private readonly char _groupSeparator;
    private readonly MethodInfo _tryParseMethodInfo;
    private readonly NumberStyles _numberStyles;

    // Kept as a CultureInfo since it is handed to the public TryParseNumber overrides and to the
    // caller supplied tryParseMethodInfo, which can both expect that exact type.
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private readonly bool _allowLeadingSign;
    private readonly bool _allowDecimalSeparator;
    private readonly bool _allowGroupSeparator;
    private readonly bool _allowExponent;

    public bool CanSeek => true;

    public char[] ExpectedChars { get; set; } = [];

    public bool SkipWhitespace => false;

    public abstract bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out T value);

    protected NumberLiteralBase(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, MethodInfo? tryParseMethodInfo = null)
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


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var resetName = $"reset{context.NextNumber()}";
        var numberSpanName = $"numberSpan{context.NextNumber()}";
        var parsedValueName = $"parsedValue{context.NextNumber()}";

        // Use direct SourceResult construction for early return optimization
        var result = new SourceResult(
            successVariable: "success",  // Not used with early returns
            valueVariable: "value",
            valueTypeName: valueTypeName);

        result.Body.Add($"var {resetName} = {cursorName}.Position;");
        result.Body.Add($"global::System.ReadOnlySpan<char> {numberSpanName} = default;");
        result.Body.Add($"{valueTypeName} {parsedValueName} = default;");

        var allowLeadingSign = _allowLeadingSign ? "true" : "false";
        var allowDecimalSeparator = _allowDecimalSeparator ? "true" : "false";
        var allowGroupSeparator = _allowGroupSeparator ? "true" : "false";
        var allowExponent = _allowExponent ? "true" : "false";

        // Emit NumberStyles as a static readonly field
        var numberStylesFieldName = context.RegisterStaticField(
            $"private static readonly global::System.Globalization.NumberStyles",
            $"(global::System.Globalization.NumberStyles){(int)_numberStyles}");
        
        // Emit CultureInfo - use InvariantCulture if it's the default, otherwise create a static field
        string cultureExpr;
        if (_culture == CultureInfo.InvariantCulture)
        {
            cultureExpr = "global::System.Globalization.CultureInfo.InvariantCulture";
        }
        else
        {
            var decimalSeparator = $"((char){(int)_decimalSeparator}).ToString()";
            var groupSeparator = $"((char){(int)_groupSeparator}).ToString()";
            cultureExpr = context.RegisterStaticField(
                "private static readonly global::System.Globalization.CultureInfo",
                $"new global::System.Func<global::System.Globalization.CultureInfo>(() => {{ var c = (global::System.Globalization.CultureInfo)global::System.Globalization.CultureInfo.InvariantCulture.Clone(); c.NumberFormat.NumberDecimalSeparator = {decimalSeparator}; c.NumberFormat.NumberGroupSeparator = {groupSeparator}; return c; }})()");
        }

        var decimalSeparatorLiteral = $"(char){(int)_decimalSeparator}";
        var groupSeparatorLiteral = $"(char){(int)_groupSeparator}";
        result.Body.Add($"if ({scannerName}.ReadDecimal({allowLeadingSign}, {allowDecimalSeparator}, {allowGroupSeparator}, {allowExponent}, out {numberSpanName}, {decimalSeparatorLiteral}, {groupSeparatorLiteral}))");
        result.Body.Add("{");
        if (context.DiscardResult)
        {
            result.Body.Add("    return true;");
        }
        else
        {
            // The helper is available in Parlot's net8.0+ assets. A net7.0 consumer selects
            // the netstandard2.0 asset, which cannot expose generic-math APIs.
            var supportsFastNumberParsing =
                context.TargetFramework.Identifier == TargetFrameworkIdentifier.NetCoreApp &&
                context.TargetFramework.Version >= new Version(8, 0);
            var tryParseMethod = supportsFastNumberParsing
                ? $"global::Parlot.Numbers.TryParseNumber<{valueTypeName}>"
                : "global::Parlot.Numbers.TryParse";
            result.Body.Add($"    if ({tryParseMethod}({numberSpanName}, {numberStylesFieldName}, {cultureExpr}, out {parsedValueName}))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.ValueVariable} = {parsedValueName};");
            result.Body.Add("        return true;");
            result.Body.Add("    }");
        }
        result.Body.Add("}");
        result.Body.Add($"{cursorName}.ResetPosition({resetName});");
        result.Body.Add($"{result.ValueVariable} = default;");
        result.Body.Add("return false;");

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

#if NET8_0_OR_GREATER
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
