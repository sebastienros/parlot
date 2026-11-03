using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Globalization;
using System.Numerics;
using System.Reflection;

namespace Parlot.Fluent;

/// <summary>
/// This class is used as a base class for custom number parsers which don't implement INumber<typeparamref name="T"/> after .NET 7.0.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class NumberLiteralBase<T> : Parser<T>, ISeekable, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly char _decimalSeparator;
    private readonly char _secondDecimalSeparator;
    private readonly char _groupSeparator;
    private readonly NumberStyles _numberStyles;

    // Kept as a CultureInfo since it is handed to the public TryParseNumber overrides and to the
    // caller supplied tryParseMethodInfo, which can both expect that exact type.
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    // Used when the number is read using the second decimal separator
    private readonly CultureInfo? _culture2;
    private readonly bool _allowLeadingSign;
    private readonly bool _allowDecimalSeparator;
    private readonly bool _allowGroupSeparator;
    private readonly bool _allowExponent;
    private readonly bool _allowUnderscore;

    public bool CanSeek => true;

    public char[] ExpectedChars { get; set; } = [];

    public bool SkipWhitespace => false;

    public abstract bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out T value);

    protected NumberLiteralBase(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, MethodInfo? tryParseMethodInfo = null)
        : this(numberOptions, decimalSeparator, groupSeparator, NumberLiterals.NoSecondDecimalSeparator, tryParseMethodInfo)
    {
    }

    protected NumberLiteralBase(NumberOptions numberOptions, char decimalSeparator, char groupSeparator, char secondDecimalSeparator, MethodInfo? tryParseMethodInfo = null)
    {
        _decimalSeparator = decimalSeparator;
        _secondDecimalSeparator = secondDecimalSeparator;
        _groupSeparator = NumberLiterals.GetEffectiveGroupSeparator(groupSeparator, secondDecimalSeparator);
        _numberStyles = numberOptions.ToNumberStyles();

        if (_groupSeparator == NumberLiterals.NoSecondDecimalSeparator)
        {
            // The group separator is the second decimal separator, it can't be both
            _numberStyles &= ~NumberStyles.AllowThousands;
        }

        if (decimalSeparator != NumberLiterals.DefaultDecimalSeparator ||
            groupSeparator != NumberLiterals.DefaultGroupSeparator ||
            _groupSeparator != groupSeparator)
        {
            _culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            _culture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();
            _culture.NumberFormat.NumberGroupSeparator = _groupSeparator == groupSeparator ? groupSeparator.ToString() : string.Empty;
        }

        if (secondDecimalSeparator != NumberLiterals.NoSecondDecimalSeparator)
        {
            _culture2 = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            _culture2.NumberFormat.NumberDecimalSeparator = secondDecimalSeparator.ToString();
            _culture2.NumberFormat.NumberGroupSeparator = _groupSeparator == groupSeparator ? groupSeparator.ToString() : string.Empty;
        }

        _allowLeadingSign = (numberOptions & NumberOptions.AllowLeadingSign) != 0;
        _allowDecimalSeparator = (numberOptions & NumberOptions.AllowDecimalSeparator) != 0;
        _allowGroupSeparator = (numberOptions & NumberOptions.AllowGroupSeparators) != 0 && _groupSeparator != NumberLiterals.NoSecondDecimalSeparator;
        _allowExponent = (numberOptions & NumberOptions.AllowExponent) != 0;
        _allowUnderscore = (numberOptions & NumberOptions.AllowUnderscore) != 0;

        var expectedChars = "0123456789";

        if (_allowLeadingSign)
        {
            expectedChars += "+-";
        }

        if (_allowDecimalSeparator)
        {
            expectedChars += _decimalSeparator;

            if (_secondDecimalSeparator != NumberLiterals.NoSecondDecimalSeparator)
            {
                expectedChars += _secondDecimalSeparator;
            }
        }

        // A group separator, an exponent or an underscore can't be a starting char

        ExpectedChars = expectedChars.ToCharArray();

        Name = "NumberLiteral";
    }

    // A derived type can override Parse()
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => GetType().Assembly == typeof(NumberLiteralBase<T>).Assembly ? false : analysis.AnalyzeUnknown(this);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var reset = context.Scanner.Cursor.Offset;
        var start = reset;

        if (context.Scanner.ReadDecimal(_allowLeadingSign, _allowDecimalSeparator, _allowGroupSeparator, _allowExponent, _allowUnderscore, out var number, _decimalSeparator, _groupSeparator, _secondDecimalSeparator))
        {
            var end = context.Scanner.Cursor.Offset;

            if (_allowUnderscore)
            {
                number = Numbers.RemoveUnderscores(number);
            }

            if (TryParseNumber(number, _numberStyles, _culture, out T value))
            {
                result.Set(start, end, value);

                context.ExitParser(this);
                return true;
            }

            if (_culture2 != null && TryParseNumber(number, _numberStyles, _culture2, out T value2))
            {
                result.Set(start, end, value2);

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

        // The generated code parses with the built-in methods, it has no parser instance to call TryParseNumber on,
        // so a custom subclass would silently parse differently from the runtime
        if (GetType().Assembly != typeof(NumberLiteralBase<T>).Assembly)
        {
            throw new NotSupportedException($"The number parser '{GetType()}' overrides TryParseNumber, which a generated parser cannot call. Only the built-in number parsers can be source generated.");
        }

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(T), () => GenerateSource(context));
        }

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

        result.Body.Add($"var {resetName} = {cursorName}.Offset;");
        result.Body.Add($"global::System.ReadOnlySpan<char> {numberSpanName} = default;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"{valueTypeName} {parsedValueName} = default;");
        }

        var (tryParseMethod, numberStylesFieldName, cultureExpr, culture2Expr) = GetParseExpressions(context);

        var allowLeadingSign = _allowLeadingSign ? "true" : "false";
        var allowDecimalSeparator = _allowDecimalSeparator ? "true" : "false";
        var allowGroupSeparator = _allowGroupSeparator ? "true" : "false";
        var allowExponent = _allowExponent ? "true" : "false";
        // The overload which takes a second decimal separator needs the underscore flag too
        var allowUnderscore = _allowUnderscore || _secondDecimalSeparator != NumberLiterals.NoSecondDecimalSeparator
            ? _allowUnderscore ? "true, " : "false, "
            : "";
        var decimalSeparatorLiteral = $"(char){(int)_decimalSeparator}";
        var groupSeparatorLiteral = $"(char){(int)_groupSeparator}";
        var secondDecimalSeparatorArgument = _secondDecimalSeparator == NumberLiterals.NoSecondDecimalSeparator
            ? ""
            : $", (char){(int)_secondDecimalSeparator}";
        result.Body.Add($"if ({scannerName}.ReadDecimal({allowLeadingSign}, {allowDecimalSeparator}, {allowGroupSeparator}, {allowExponent}, {allowUnderscore}out {numberSpanName}, {decimalSeparatorLiteral}, {groupSeparatorLiteral}{secondDecimalSeparatorArgument}))");
        result.Body.Add("{");
        if (_allowUnderscore)
        {
            result.Body.Add($"    {numberSpanName} = global::Parlot.Numbers.RemoveUnderscores({numberSpanName});");
        }
        var outTarget = context.DiscardResult ? $"{valueTypeName} _" : parsedValueName;

        // The number is parsed using the second decimal separator if it can't be parsed using the first one
        var cultures = culture2Expr == null ? new[] { cultureExpr } : new[] { cultureExpr, culture2Expr };

        foreach (var culture in cultures)
        {
            result.Body.Add($"    if ({tryParseMethod}({numberSpanName}, {numberStylesFieldName}, {culture}, out {outTarget}))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.ValueVariable} = {(context.DiscardResult ? "default" : parsedValueName)};");
            result.Body.Add("        return true;");
            result.Body.Add("    }");
        }
        result.Body.Add("}");
        result.Body.Add($"{cursorName}.ResetPosition({resetName});");
        result.Body.Add($"{result.ValueVariable} = default;");
        result.Body.Add("return false;");

        return result;
    }

    // The fields registered in the last context, which span code uses each time the parser is inlined
    private (SourceGenerationContext Context, string TryParseMethod, string NumberStyles, string Culture, string? Culture2)? _parseExpressions;

    // Returns the method parsing the number, and the expressions of its styles and culture
    private (string TryParseMethod, string NumberStyles, string Culture, string? Culture2) GetParseExpressions(SourceGenerationContext context)
    {
        if (_parseExpressions is { } expressions && ReferenceEquals(expressions.Context, context))
        {
            return (expressions.TryParseMethod, expressions.NumberStyles, expressions.Culture, expressions.Culture2);
        }

        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Emit NumberStyles as a static readonly field
        var numberStylesFieldName = context.RegisterStaticField(
            $"private static readonly global::System.Globalization.NumberStyles",
            $"(global::System.Globalization.NumberStyles){(int)_numberStyles}");

        // Emit CultureInfo - use InvariantCulture if it's the default, otherwise create a static field
        var cultureExpr = GetCultureExpression(context, _culture, _decimalSeparator);
        var culture2Expr = _culture2 == null ? null : GetCultureExpression(context, _culture2, _secondDecimalSeparator);

        // The helper is available in Parlot's net8.0+ assets. A net7.0 consumer selects
        // the netstandard2.0 asset, which cannot expose generic-math APIs, and C# 10 can't compile them.
        var supportsFastNumberParsing =
            context.TargetFramework.Identifier == TargetFrameworkIdentifier.NetCoreApp &&
            context.TargetFramework.Version >= new Version(8, 0) &&
            context.SupportsStaticAbstractMembers;
        var tryParseMethod = supportsFastNumberParsing
            ? $"global::Parlot.Numbers.TryParseNumber<{valueTypeName}>"
            : Numbers.HasTryParseOverload(typeof(T))
                ? "global::Parlot.Numbers.TryParse"
                : throw new NotSupportedException($"Source generation for numbers of type '{typeof(T)}' requires .NET 8 or later and C# 11 or later.");

        _parseExpressions = (context, tryParseMethod, numberStylesFieldName, cultureExpr, culture2Expr);

        return (tryParseMethod, numberStylesFieldName, cultureExpr, culture2Expr);
    }

    private static string GetCultureExpression(SourceGenerationContext context, CultureInfo culture, char decimalSeparator)
    {
        if (culture == CultureInfo.InvariantCulture)
        {
            return "global::System.Globalization.CultureInfo.InvariantCulture";
        }

        var decimalSeparatorExpr = $"((char){(int)decimalSeparator}).ToString()";
        var groupSeparatorExpr = culture.NumberFormat.NumberGroupSeparator.Length == 0
            ? "string.Empty"
            : $"((char){(int)culture.NumberFormat.NumberGroupSeparator[0]}).ToString()";
        return context.RegisterStaticField(
            "private static readonly global::System.Globalization.CultureInfo",
            $"new global::System.Func<global::System.Globalization.CultureInfo>(() => {{ var c = (global::System.Globalization.CultureInfo)global::System.Globalization.CultureInfo.InvariantCulture.Clone(); c.NumberFormat.NumberDecimalSeparator = {decimalSeparatorExpr}; c.NumberFormat.NumberGroupSeparator = {groupSeparatorExpr}; return c; }})()");
    }

    // Only the built-in number parsers can be generated, see GenerateSource. The code parsing the value is large, a
    // subgraph only inlines a few.
    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context)
        => GetType().Assembly == typeof(NumberLiteralBase<T>).Assembly ? 8 : -1;

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var (tryParseMethod, numberStyles, culture, culture2) = GetParseExpressions(writer.Context);

        NumberSource.GenerateSpanSource(writer, valueVariable, fail, typeof(T), tryParseMethod, numberStyles, culture, culture2,
            _allowLeadingSign, _allowDecimalSeparator, _allowGroupSeparator, _allowExponent, _allowUnderscore,
            _decimalSeparator, _groupSeparator, _secondDecimalSeparator);
    }
}

internal sealed class ByteNumberLiteral : NumberLiteralBase<byte>
{
    public ByteNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out byte value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class SByteNumberLiteral : NumberLiteralBase<sbyte>
{
    public SByteNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out sbyte value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class IntNumberLiteral : NumberLiteralBase<int>
{
    public IntNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out int value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class UIntNumberLiteral : NumberLiteralBase<uint>
{
    public UIntNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out uint value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class LongNumberLiteral : NumberLiteralBase<long>
{
    public LongNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out long value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class ULongNumberLiteral : NumberLiteralBase<ulong>
{
    public ULongNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ulong value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class ShortNumberLiteral : NumberLiteralBase<short>
{
    public ShortNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out short value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class UShortNumberLiteral : NumberLiteralBase<ushort>
{
    public UShortNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ushort value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class DecimalNumberLiteral : NumberLiteralBase<decimal>
{
    public DecimalNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out decimal value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class DoubleNumberLiteral : NumberLiteralBase<double>
{
    public DoubleNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out double value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class FloatNumberLiteral : NumberLiteralBase<float>
{
    public FloatNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out float value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}

internal sealed class BigIntegerNumberLiteral : NumberLiteralBase<BigInteger>
{
    public BigIntegerNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, char secondDecimalSeparator = NumberLiterals.NoSecondDecimalSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator, secondDecimalSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out BigInteger value)
    {
        return Numbers.TryParse(s, style, provider, out value);
    }
}
