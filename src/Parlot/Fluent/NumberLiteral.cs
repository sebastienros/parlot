#if NET8_0_OR_GREATER
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Globalization;
using System.Numerics;

namespace Parlot.Fluent;

public sealed class NumberLiteral<T> : Parser<T>, ISeekable, ISourceable, ILoopAnalyzable, ISpanSourceable, IInvokesOnlyLiterals
    where T : INumber<T>
{
    private const char DefaultDecimalSeparator = '.';
    private const char DefaultGroupSeparator = ',';

    private readonly char _decimalSeparator;
    private readonly char _groupSeparator;
    private readonly NumberStyles _numberStyles;

    // A NumberFormatInfo is stored instead of a CultureInfo since NumberFormatInfo.GetInstance()
    // returns it directly, while a CultureInfo needs to be resolved on every call.
    private readonly NumberFormatInfo _culture = CultureInfo.InvariantCulture.NumberFormat;
    private readonly bool _allowLeadingSign;
    private readonly bool _allowDecimalSeparator;
    private readonly bool _allowGroupSeparator;
    private readonly bool _allowExponent;
    private readonly bool _allowUnderscore;
    private readonly bool _requireFractionalPartForDecimals;

    public bool CanSeek { get; } = true;

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    public NumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = DefaultDecimalSeparator, char groupSeparator = DefaultGroupSeparator)
    {
        _decimalSeparator = decimalSeparator;
        _groupSeparator = groupSeparator;
        _numberStyles = numberOptions.ToNumberStyles();

        if (decimalSeparator != NumberLiterals.DefaultDecimalSeparator ||
            groupSeparator != NumberLiterals.DefaultGroupSeparator)
        {
            _culture = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            _culture.NumberDecimalSeparator = decimalSeparator.ToString();
            _culture.NumberGroupSeparator = groupSeparator.ToString();
        }

        _allowLeadingSign = (numberOptions & NumberOptions.AllowLeadingSign) != 0;
        _allowDecimalSeparator = (numberOptions & NumberOptions.AllowDecimalSeparator) != 0;
        _allowGroupSeparator = (numberOptions & NumberOptions.AllowGroupSeparators) != 0;
        _allowExponent = (numberOptions & NumberOptions.AllowExponent) != 0;
        _allowUnderscore = (numberOptions & NumberOptions.AllowUnderscore) != 0;
        _requireFractionalPartForDecimals = (numberOptions & NumberOptions.RequireFractionalPartForDecimals) != 0;

        ExpectedChars = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

        if (_allowLeadingSign)
        {
            ExpectedChars = [.. ExpectedChars, '+', '-'];
        }

        if (_allowDecimalSeparator)
        {
            ExpectedChars = [.. ExpectedChars, decimalSeparator];
        }

        // A group separator, an exponent or an underscore can't be a starting char

        Name = "NumberLiteral";
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => false;

    // It reads its chars only, see IInvokesOnlyLiterals
    bool IInvokesOnlyLiterals.InvokesOnlyLiterals => true;

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var reset = context.Scanner.Cursor.Offset;
        var start = reset;

        if (context.Scanner.ReadDecimal(_allowLeadingSign, _allowDecimalSeparator, _allowGroupSeparator, _allowExponent, _allowUnderscore, _requireFractionalPartForDecimals, out var number, _decimalSeparator, _groupSeparator))
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
        var numberSpanName = $"numberSpan{context.NextNumber()}";

        result.Body.Add($"var {resetName} = {cursorName}.Offset;");

        var allowLeadingSign = _allowLeadingSign ? "true" : "false";
        var allowDecimalSeparator = _allowDecimalSeparator ? "true" : "false";
        var allowGroupSeparator = _allowGroupSeparator ? "true" : "false";
        var allowExponent = _allowExponent ? "true" : "false";

        var (tryParseMethod, numberStylesExpr, cultureExpr, downlevel) = GetParseExpressions(context);

        var decimalSeparatorLiteral = $"(char){(int)_decimalSeparator}";
        var groupSeparatorLiteral = $"(char){(int)_groupSeparator}";
        var allowUnderscore = _allowUnderscore ? "true" : "false";
        var requireFractionalPartForDecimals = _requireFractionalPartForDecimals ? "true" : "false";
        result.Body.Add($"if ({scannerName}.ReadDecimal({allowLeadingSign}, {allowDecimalSeparator}, {allowGroupSeparator}, {allowExponent}, {allowUnderscore}, {requireFractionalPartForDecimals}, out var {numberSpanName}, {decimalSeparatorLiteral}, {groupSeparatorLiteral}))");
        result.Body.Add("{");
        if (_allowUnderscore)
        {
            result.Body.Add($"    {numberSpanName} = global::Parlot.Numbers.RemoveUnderscores({numberSpanName});");
        }

        var outTarget = context.DiscardResult ? (downlevel ? $"{valueTypeName} _" : "_") : result.ValueVariable;
        result.Body.Add($"    if ({tryParseMethod}({numberSpanName}, {numberStylesExpr}, {cultureExpr}, out {outTarget}))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({resetName});");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => 8;

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var (tryParseMethod, numberStylesExpr, cultureExpr, _) = GetParseExpressions(writer.Context);

        NumberSource.GenerateSpanSource(writer, valueVariable, fail, typeof(T), tryParseMethod, numberStylesExpr, cultureExpr,
            _allowLeadingSign, _allowDecimalSeparator, _allowGroupSeparator, _allowExponent, _allowUnderscore, _requireFractionalPartForDecimals,
            _decimalSeparator, _groupSeparator);
    }

    private (SourceGenerationContext Context, string TryParseMethod, string NumberStylesExpr, string CultureExpr, bool Downlevel)? _parseExpressions;

    private (string TryParseMethod, string NumberStylesExpr, string CultureExpr, bool Downlevel) GetParseExpressions(SourceGenerationContext context)
    {
        if (_parseExpressions is { } expressions && ReferenceEquals(expressions.Context, context))
        {
            return (expressions.TryParseMethod, expressions.NumberStylesExpr, expressions.CultureExpr, expressions.Downlevel);
        }

        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var numberStylesExpr = $"(global::System.Globalization.NumberStyles){(int)_numberStyles}";

        var cultureExpr = "global::System.Globalization.NumberFormatInfo.InvariantInfo";
        if (!ReferenceEquals(_culture, CultureInfo.InvariantCulture.NumberFormat))
        {
            var decimalSeparator = $"((char){(int)_decimalSeparator}).ToString()";
            var groupSeparator = $"((char){(int)_groupSeparator}).ToString()";
            cultureExpr = context.RegisterStaticField(
                "private static readonly global::System.Globalization.NumberFormatInfo",
                $"new global::System.Func<global::System.Globalization.NumberFormatInfo>(() => {{ var c = (global::System.Globalization.NumberFormatInfo)global::System.Globalization.NumberFormatInfo.InvariantInfo.Clone(); c.NumberDecimalSeparator = {decimalSeparator}; c.NumberGroupSeparator = {groupSeparator}; return c; }})()");
        }

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

        _parseExpressions = (context, tryParseMethod, numberStylesExpr, cultureExpr, downlevel);
        return (tryParseMethod, numberStylesExpr, cultureExpr, downlevel);
    }
}
#endif
