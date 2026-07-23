#if NET8_0_OR_GREATER
using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class NumberLiteral<T> : Parser<T>, ICompilable, ISeekable
    where T : INumber<T>
{
    private const char DefaultDecimalSeparator = '.';
    private const char DefaultGroupSeparator = ',';

    private static readonly MethodInfo _tryParseMethodInfo = typeof(NumberLiteral<T>).GetMethod(nameof(TryParseNumber), BindingFlags.Static | BindingFlags.NonPublic)!;

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

            if (TryParseNumber(number, _numberStyles, _culture, out var value))
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

    /// <summary>
    /// The number of digits that always fit in a <see cref="long"/>, <c>long.MaxValue</c> has 19 of them.
    /// </summary>
    private const int MaxFastDigits = 18;

    /// <summary>
    /// Parses a number, using a dedicated implementation for the plain sequences of digits that make up
    /// most of the numbers found in a grammar.
    /// </summary>
    /// <remarks>
    /// The general purpose parser has to handle everything <see cref="NumberStyles"/> allows, which is a
    /// significant part of the parsing time even for a single digit. Anything that is not a short sequence
    /// of digits, e.g. a sign, a decimal separator or an exponent, falls back to it.
    /// </remarks>
    internal static bool TryParseNumber(ReadOnlySpan<char> span, NumberStyles styles, NumberFormatInfo culture, [MaybeNullWhen(false)] out T value)
    {
        if ((uint)(span.Length - 1) < MaxFastDigits)
        {
            long parsed = 0;

            foreach (var c in span)
            {
                var digit = (uint)(c - '0');

                if (digit > 9)
                {
                    return T.TryParse(span, styles, culture, out value);
                }

                parsed = (parsed * 10) + digit;
            }

            value = T.CreateTruncating(parsed);

            // T can be narrower than a long, e.g. Terms.Byte() reading "300", in which case the
            // conversion silently wrapped and the general purpose parser decides whether it is valid.
            if (long.CreateTruncating(value) == parsed)
            {
                return true;
            }
        }

        return T.TryParse(span, styles, culture, out value);
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // var reset = context.Scanner.Cursor.Position;

        var reset = context.DeclarePositionVariable(result);

        var numberStyles = result.DeclareVariable<NumberStyles>($"numberStyles{context.NextNumber}", Expression.Constant(_numberStyles));
        var culture = result.DeclareVariable<NumberFormatInfo>($"culture{context.NextNumber}", Expression.Constant(_culture));
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
}
#endif
