using Parlot.Compilation;
using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Parlot.Fluent
{
    /// <summary>
    /// This class is used as a base class for custom number parsers which don't implement INumber<typeparamref name="T"/> after .NET 7.0.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class NumberLiteralBase<T> : Parser<T>, ICompilable
    {
        private static readonly MethodInfo _defaultTryParseMethodInfo = typeof(T).GetMethod("TryParse", [typeof(string), typeof(NumberStyles), typeof(IFormatProvider), typeof(T).MakeByRefType()]);
        private static readonly MethodInfo _rosToString = typeof(ReadOnlySpan<char>).GetMethod(nameof(ToString), []);

        private readonly char _decimalSeparator;
        private readonly char _groupSeparator;
        private readonly MethodInfo _tryParseMethodInfo;
        private readonly NumberStyles _numberStyles;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
        private readonly bool _allowLeadingSign;
        private readonly bool _allowDecimalSeparator;
        private readonly bool _allowGroupSeparator;
        private readonly bool _allowExponent;

        private delegate (bool, T) TryParseDelegate();

        public abstract bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out T value);
        
        public NumberLiteralBase(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiterals.DefaultDecimalSeparator, char groupSeparator = NumberLiterals.DefaultGroupSeparator, MethodInfo tryParseMethodInfo = null)
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
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var reset = context.Scanner.Cursor.Position;
            var start = reset.Offset;

            if (context.Scanner.ReadDecimal(_allowLeadingSign, _allowDecimalSeparator, _allowGroupSeparator, _allowExponent, out var number, _decimalSeparator, _groupSeparator))
            {
                var end = context.Scanner.Cursor.Offset;

                var sourceToParse = number.ToString();

                if (TryParseNumber(number, _numberStyles, _culture, out T value))
                {
                    result.Set(start, end, value);
                    return true;
                }
            }

            context.Scanner.Cursor.ResetPosition(reset);

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // var start = context.Scanner.Cursor.Offset;
            // var reset = context.Scanner.Cursor.Position;

            var start = context.DeclareOffsetVariable(result);
            var reset = context.DeclarePositionVariable(result);

            var numberStyles = context.DeclareVariable<NumberStyles>(result, $"numberStyles{context.NextNumber}", Expression.Constant(_numberStyles));
            var culture = context.DeclareVariable<CultureInfo>(result, $"culture{context.NextNumber}", Expression.Constant(_culture));
            var numberSpan = context.DeclareVariable(result, $"number{context.NextNumber}", typeof(ReadOnlySpan<char>));
            var end = context.DeclareVariable<int>(result, $"end{context.NextNumber}");

            // if (context.Scanner.ReadDecimal(_numberOptions, out var numberSpan, _decimalSeparator, _groupSeparator))
            // {
            //    var end = context.Scanner.Cursor.Offset;
            //    success = T.TryParse(numberSpan.ToString(), numberStyles, culture, out var value));
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
                        Expression.Assign(success,
                            Expression.Call(
                                _tryParseMethodInfo,
                                Expression.Call(numberSpan, _rosToString),
                                numberStyles,
                                culture,
                                value)
                            )
                    )
                );

            result.Body.Add(block);

            result.Body.Add(
                Expression.IfThen(
                    Expression.Not(success),
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
            return byte.TryParse(s.ToString(), style, provider, out value );
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
            return sbyte.TryParse(s.ToString(), style, provider, out value);
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
            return int.TryParse(s.ToString(), style, provider, out value);
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
            return uint.TryParse(s.ToString(), style, provider, out value);
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
            return long.TryParse(s.ToString(), style, provider, out value);
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
            return ulong.TryParse(s.ToString(), style, provider, out value);
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
            return short.TryParse(s.ToString(), style, provider, out value);
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
            return ushort.TryParse(s.ToString(), style, provider, out value);
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
            return decimal.TryParse(s.ToString(), style, provider, out value);
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
            return double.TryParse(s.ToString(), style, provider, out value);
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
            return float.TryParse(s.ToString(), style, provider, out value);
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
            return Half.TryParse(s.ToString(), style, provider, out value);
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
            return BigInteger.TryParse(s.ToString(), style, provider, out value);
        }
    }
}
