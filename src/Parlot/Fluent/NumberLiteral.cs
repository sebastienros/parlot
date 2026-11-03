#if NET8_0_OR_GREATER
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Globalization;
using System.Numerics;

namespace Parlot.Fluent;

public sealed class NumberLiteral<T> : Parser<T>, ISeekable, ISourceable, ILoopAnalyzable
    where T : INumber<T>
{
    private const char DefaultDecimalSeparator = '.';
    private const char DefaultGroupSeparator = ',';

    private readonly char _decimalSeparator;
    private readonly char _secondDecimalSeparator;
    private readonly char _groupSeparator;
    private readonly NumberStyles _numberStyles;

    // A NumberFormatInfo is stored instead of a CultureInfo since NumberFormatInfo.GetInstance()
    // returns it directly, while a CultureInfo needs to be resolved on every call.
    private readonly NumberFormatInfo _culture = CultureInfo.InvariantCulture.NumberFormat;

    // Used when the number is read using the second decimal separator
    private readonly NumberFormatInfo? _culture2;
    private readonly bool _allowLeadingSign;
    private readonly bool _allowDecimalSeparator;
    private readonly bool _allowGroupSeparator;
    private readonly bool _allowExponent;
    private readonly bool _allowUnderscore;

    public bool CanSeek { get; } = true;

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    public NumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = DefaultDecimalSeparator, char groupSeparator = DefaultGroupSeparator)
        : this(numberOptions, decimalSeparator, groupSeparator, NumberLiterals.NoSecondDecimalSeparator)
    {
    }

    /// <summary>
    /// Creates a number literal which also accepts a second decimal separator.
    /// </summary>
    /// <param name="numberOptions">The options of the number.</param>
    /// <param name="decimalSeparator">The decimal separator.</param>
    /// <param name="groupSeparator">The group separator. It is not used if it is the same char as <paramref name="secondDecimalSeparator"/>.</param>
    /// <param name="secondDecimalSeparator">
    /// An alternative decimal separator, or <c>'\0'</c> if there is none. The number is parsed using
    /// <paramref name="decimalSeparator"/> and, if that fails, using this one.
    /// </param>
    public NumberLiteral(NumberOptions numberOptions, char decimalSeparator, char groupSeparator, char secondDecimalSeparator)
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
            _culture = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            _culture.NumberDecimalSeparator = decimalSeparator.ToString();
            _culture.NumberGroupSeparator = _groupSeparator == groupSeparator ? groupSeparator.ToString() : string.Empty;
        }

        if (secondDecimalSeparator != NumberLiterals.NoSecondDecimalSeparator)
        {
            _culture2 = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            _culture2.NumberDecimalSeparator = secondDecimalSeparator.ToString();
            _culture2.NumberGroupSeparator = _groupSeparator == groupSeparator ? groupSeparator.ToString() : string.Empty;
        }

        _allowLeadingSign = (numberOptions & NumberOptions.AllowLeadingSign) != 0;
        _allowDecimalSeparator = (numberOptions & NumberOptions.AllowDecimalSeparator) != 0;
        _allowGroupSeparator = (numberOptions & NumberOptions.AllowGroupSeparators) != 0 && _groupSeparator != NumberLiterals.NoSecondDecimalSeparator;
        _allowExponent = (numberOptions & NumberOptions.AllowExponent) != 0;
        _allowUnderscore = (numberOptions & NumberOptions.AllowUnderscore) != 0;

        ExpectedChars = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

        if (_allowLeadingSign)
        {
            ExpectedChars = [.. ExpectedChars, '+', '-'];
        }

        if (_allowDecimalSeparator)
        {
            ExpectedChars = [.. ExpectedChars, decimalSeparator];

            if (secondDecimalSeparator != NumberLiterals.NoSecondDecimalSeparator)
            {
                ExpectedChars = [.. ExpectedChars, secondDecimalSeparator];
            }
        }

        // A group separator, an exponent or an underscore can't be a starting char

        Name = "NumberLiteral";
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => false;

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

            if (Numbers.TryParseNumber<T>(number, _numberStyles, _culture, out var value))
            {
                result.Set(start, end, value);

                context.ExitParser(this);
                return true;
            }

            if (_culture2 != null && Numbers.TryParseNumber<T>(number, _numberStyles, _culture2, out var value2))
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

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(T), () => GenerateSource(context));
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var resetName = $"reset{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";
        var numberSpanName = $"numberSpan{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";
        var parsedValueName = $"parsedValue{context.NextNumber()}";

        result.Body.Add($"var {resetName} = 0;");
        result.Body.Add($"var {startName} = 0;");
        result.Body.Add($"global::System.ReadOnlySpan<char> {numberSpanName} = default;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"{valueTypeName} {parsedValueName} = default;");
        }

        result.Body.Add($"{result.SuccessVariable} = false;");
        result.Body.Add($"{resetName} = {cursorName}.Offset;");
        result.Body.Add($"{startName} = {resetName};");

        var allowLeadingSign = _allowLeadingSign ? "true" : "false";
        var allowDecimalSeparator = _allowDecimalSeparator ? "true" : "false";
        var allowGroupSeparator = _allowGroupSeparator ? "true" : "false";
        var allowExponent = _allowExponent ? "true" : "false";
        // The overload which takes a second decimal separator needs the underscore flag too
        var allowUnderscore = _allowUnderscore || _secondDecimalSeparator != NumberLiterals.NoSecondDecimalSeparator
            ? _allowUnderscore ? "true, " : "false, "
            : "";

        // Emit NumberStyles as a literal cast
        var numberStylesExpr = $"(global::System.Globalization.NumberStyles){(int)_numberStyles}";

        var cultureExpr = GetCultureExpression(context, _culture, _decimalSeparator);
        var culture2Expr = _culture2 == null ? null : GetCultureExpression(context, _culture2, _secondDecimalSeparator);

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
        var outTarget = context.DiscardResult ? "_" : parsedValueName;
        // The generic helper needs the consumer's net8.0+ runtime and C# 11; other consumers only have the fixed
        // overloads. An unknown target keeps the generic path this build was compiled for.
        var downlevel = !context.SupportsStaticAbstractMembers || context.TargetFramework.Identifier switch
        {
            TargetFrameworkIdentifier.NetFramework or TargetFrameworkIdentifier.NetStandard => true,
            TargetFrameworkIdentifier.NetCoreApp => context.TargetFramework.Version < new Version(8, 0),
            _ => false,
        };
        var tryParseMethod = !downlevel
            ? $"global::Parlot.Numbers.TryParseNumber<{valueTypeName}>"
            : Numbers.HasTryParseOverload(typeof(T))
                ? "global::Parlot.Numbers.TryParse"
                : throw new NotSupportedException($"Source generation for numbers of type '{typeof(T)}' requires .NET 8 or later and C# 11 or later.");

        // The number is parsed using the second decimal separator if it can't be parsed using the first one
        var cultures = culture2Expr == null ? new[] { cultureExpr } : new[] { cultureExpr, culture2Expr };

        for (var i = 0; i < cultures.Length; i++)
        {
            result.Body.Add($"    {(i == 0 ? "if" : "else if")} ({tryParseMethod}({numberSpanName}, {numberStylesExpr}, {cultures[i]}, out {outTarget}))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            if (!context.DiscardResult)
            {
                result.Body.Add($"        {result.ValueVariable} = {parsedValueName};");
            }
            result.Body.Add("    }");
        }
        result.Body.Add("}");

        result.Body.Add($"if (!{result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({resetName});");
        result.Body.Add("}");

        return result;
    }

    private static string GetCultureExpression(SourceGenerationContext context, NumberFormatInfo culture, char decimalSeparator)
    {
        if (ReferenceEquals(culture, CultureInfo.InvariantCulture.NumberFormat))
        {
            return "global::System.Globalization.CultureInfo.InvariantCulture.NumberFormat";
        }

        var decimalSeparatorExpr = $"((char){(int)decimalSeparator}).ToString()";
        var groupSeparatorExpr = culture.NumberGroupSeparator.Length == 0
            ? "string.Empty"
            : $"((char){(int)culture.NumberGroupSeparator[0]}).ToString()";
        return context.RegisterStaticField(
            "private static readonly global::System.Globalization.NumberFormatInfo",
            $"new global::System.Func<global::System.Globalization.NumberFormatInfo>(() => {{ var c = (global::System.Globalization.NumberFormatInfo)global::System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone(); c.NumberDecimalSeparator = {decimalSeparatorExpr}; c.NumberGroupSeparator = {groupSeparatorExpr}; return c; }})()");
    }
}
#endif
