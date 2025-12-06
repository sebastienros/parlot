#if NET8_0_OR_GREATER
using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class NumberLiteral<T> : Parser<T>, ICompilable, ISeekable, ISourceable
    where T : INumber<T>
{
    private const char DefaultDecimalSeparator = '.';
    private const char DefaultGroupSeparator = ',';

    private static readonly MethodInfo _tryParseMethodInfo = typeof(T).GetMethod(nameof(INumber<T>.TryParse), [typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(T).MakeByRefType()])!;

    private readonly char _decimalSeparator;
    private readonly char _groupSeparator;
    private readonly NumberStyles _numberStyles;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private readonly bool _allowLeadingSign;
    private readonly bool _allowDecimalSeparator;
    private readonly bool _allowGroupSeparator;
    private readonly bool _allowExponent;

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
            _culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            _culture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();
            _culture.NumberFormat.NumberGroupSeparator = groupSeparator.ToString();
        }

        _allowLeadingSign = (numberOptions & NumberOptions.AllowLeadingSign) != 0;
        _allowDecimalSeparator = (numberOptions & NumberOptions.AllowDecimalSeparator) != 0;
        _allowGroupSeparator = (numberOptions & NumberOptions.AllowGroupSeparators) != 0;
        _allowExponent = (numberOptions & NumberOptions.AllowExponent) != 0;

        ExpectedChars = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

        if (_allowLeadingSign)
        {
            ExpectedChars = [.. ExpectedChars, '+', '-'];
        }

        if (_allowDecimalSeparator)
        {
            ExpectedChars = [.. ExpectedChars, decimalSeparator];
        }

        if (_allowGroupSeparator)
        {
            ExpectedChars = [.. ExpectedChars, groupSeparator];
        }

        // Exponent can't be a starting char

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

            if (T.TryParse(number, _numberStyles, _culture, out var value))
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
        //    success = T.TryParse(numberSpan, numberStyles, culture, out var value));
        // }
        //
        // if (!success)
        // {
        //    context.Scanner.Cursor.ResetPosition(begin);
        // }
        //

        //var sourceToParse = Expression.Variable(typeof(ReadOnlySpan<char>), $"sourceToParse{context.NextNumber}");
        //var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(typeof(MemoryExtensions).GetMethod("AsSpan", new[] { typeof(string), typeof(int), typeof(int) }), context.Buffer(), start, Expression.Subtract(end, start)));

        var block =
            Expression.IfThen(
                context.ReadDecimal(
                    Expression.Constant(_allowLeadingSign),
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
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var resetName = $"reset{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";
        var numberSpanName = $"numberSpan{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";
        var parsedValueName = $"parsedValue{context.NextNumber()}";

        result.Body.Add($"var {resetName} = default(global::Parlot.TextPosition);");
        result.Body.Add($"var {startName} = 0;");
        result.Body.Add($"global::System.ReadOnlySpan<char> {numberSpanName} = default;");
        result.Body.Add($"var {endName} = 0;");
        result.Body.Add($"{valueTypeName} {parsedValueName} = default;");

        result.Body.Add($"{result.SuccessVariable} = false;");
        result.Body.Add($"{resetName} = {ctx}.Scanner.Cursor.Position;");
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

        result.Body.Add($"if ({ctx}.Scanner.ReadDecimal({allowLeadingSign}, {allowDecimalSeparator}, {allowGroupSeparator}, {allowExponent}, out {numberSpanName}, '{_decimalSeparator}', '{_groupSeparator}'))");
        result.Body.Add("{");
        result.Body.Add($"    {endName} = {ctx}.Scanner.Cursor.Offset;");
        result.Body.Add($"    if ({valueTypeName}.TryParse({numberSpanName}, {numberStylesExpr}, {cultureExpr}, out {parsedValueName}))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = {parsedValueName};");
        result.Body.Add("    }");
        result.Body.Add("}");

        result.Body.Add($"if (!{result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {ctx}.Scanner.Cursor.ResetPosition({resetName});");
        result.Body.Add("}");

        return result;
    }
}
#endif
