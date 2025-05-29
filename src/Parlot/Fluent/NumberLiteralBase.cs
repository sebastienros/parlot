using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

using Parlot.Compilation;
using Parlot.Rewriting;

namespace Parlot.Fluent;

/// <summary>
/// This class is used as a base class for custom number parsers which don't implement INumber<typeparamref name="T"/> after .NET 7.0.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class NumberLiteralBase<T> : Parser<T>, ICompilable, ISeekable
{
    private static readonly MethodInfo _defaultTryParseMethodInfo = typeof(T).GetMethod("TryParse", [typeof(string), typeof(NumberStyles), typeof(IFormatProvider), typeof(T).MakeByRefType()])!;

    private readonly char _decimalSeparator;
    private readonly char _groupSeparator;
    private readonly MethodInfo _tryParseMethodInfo;
    private readonly NumberStyles _numberStyles;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private readonly bool _allowLeadingSign;
    private readonly bool _allowDecimalSeparator;
    private readonly bool _allowGroupSeparator;
    private readonly bool _allowExponent;
    private readonly bool _allowUnderscore;
    public bool CanSeek => true;

    public char[] ExpectedChars { get; set; } = [];

    public bool SkipWhitespace => false;

    public abstract bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out T value);

    public NumberLiteralBase(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, MethodInfo? tryParseMethodInfo = null)
    {
        _decimalSeparator = decimalSeparator;
        _groupSeparator = groupSeparator;
        _tryParseMethodInfo = tryParseMethodInfo ?? _defaultTryParseMethodInfo;
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
        _allowUnderscore = (numberOptions & NumberOptions.AllowUnderscore) != 0;

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

        if (_allowUnderscore)
        {
            expectedChars += "_";
        }

        ExpectedChars = expectedChars.ToCharArray();

        Name = "NumberLiteral";
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var reset = context.Scanner.Cursor.Position;
        var start = reset.Offset;

        if (context.Scanner.ReadDecimal(_allowLeadingSign, _allowDecimalSeparator, _allowGroupSeparator, _allowExponent, _allowUnderscore, out var number, _decimalSeparator, _groupSeparator))
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
                    Expression.Constant(_allowUnderscore),
                    numberSpan, Expression.Constant(_decimalSeparator), Expression.Constant(_groupSeparator)),
                Expression.Block(
                    Expression.Assign(end, context.Offset()),
                    Expression.Assign(result.Success,
                        Expression.Call(
                            _tryParseMethodInfo,
                            // This class is only used before NET7.0, when there is no overload for TryParse that takes a ReadOnlySpan<char>
                            Expression.Call(numberSpan, ExpressionHelper.ReadOnlySpan_ToString),
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

internal sealed class ByteNumberLiteral : NumberLiteralBase<byte>
{
    public ByteNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator)
        : base(numberOptions, decimalSeparator, groupSeparator)
    {

    }

    public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out byte value)
    {
#if NET6_0_OR_GREATER
        return byte.TryParse(s, style, provider, out value);
#else
        return byte.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return sbyte.TryParse(s, style, provider, out value);
#else
        return sbyte.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return int.TryParse(s, style, provider, out value);
#else
        return int.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return uint.TryParse(s, style, provider, out value);
#else
        return uint.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return long.TryParse(s, style, provider, out value);
#else
        return long.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return ulong.TryParse(s, style, provider, out value);
#else
        return ulong.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return short.TryParse(s, style, provider, out value);
#else
        return short.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return ushort.TryParse(s, style, provider, out value);
#else
        return ushort.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return decimal.TryParse(s, style, provider, out value);
#else
        return decimal.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return double.TryParse(s, style, provider, out value);
#else
        return double.TryParse(s.ToString(), style, provider, out value);
#endif
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
#if NET6_0_OR_GREATER
        return float.TryParse(s, style, provider, out value);
#else
        return float.TryParse(s.ToString(), style, provider, out value);
#endif
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
        return Half.TryParse(s, style, provider, out value);
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
#if NET6_0_OR_GREATER
        return BigInteger.TryParse(s, style, provider, out value);
#else
        return BigInteger.TryParse(s.ToString(), style, provider, out value);
#endif
    }
}
