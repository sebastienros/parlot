#if true
#if !NET8_0_OR_GREATER
using Parlot.Compilation;
using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Parlot.Fluent
{
    public static class NumberLiteral
    {
        public const char DefaultDecimalSeparator = '.';
        public const char DefaultGroupSeparator = ',';

        public static NumberLiteral<T> CreateNumberLiteralParser<T>(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = DefaultDecimalSeparator, char groupSeparator = DefaultGroupSeparator)
        {
            if (typeof(T) == typeof(byte))
            {
                var literal = new ByteNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(sbyte))
            {
                var literal = new SByteNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(int))
            {
                var literal = new IntNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(uint))
            {
                var literal = new UIntNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(long))
            {
                var literal = new LongNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(ulong))
            {
                var literal = new ULongNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(short))
            {
                var literal = new ShortNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(ushort))
            {
                var literal = new UShortNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(decimal))
            {
                var literal = new DecimalNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(double))
            {
                var literal = new DoubleNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else if (typeof(T) == typeof(float))
            {
                var literal = new FloatNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
#if NET6_0_OR_GREATER
            else if (typeof(T) == typeof(Half))
            {   
                var literal = new HalfNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
#endif
            else if (typeof(T) == typeof(BigInteger))
            {
                var literal = new BigIntegerNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
                return literal as NumberLiteral<T>;
            }
            else
            {
                throw new NotSupportedException($"The type '{typeof(T)}' is not supported as a type argument for '{nameof(NumberLiteral<T>)}'. Only numeric types are allowed.");
            }
        }
    }

    public abstract class NumberLiteral<T> : Parser<T>, ICompilable
    {
        private static readonly MethodInfo _tryParseMethodInfo = typeof(T).GetMethod("TryParse", [typeof(string), typeof(NumberStyles), typeof(IFormatProvider), typeof(T).MakeByRefType()]);
        private static readonly MethodInfo _rosToString = typeof(ReadOnlySpan<char>).GetMethod(nameof(ToString), []);

        private readonly NumberOptions _numberOptions;
        private readonly char _decimalSeparator;
        private readonly char _groupSeparator;
        private readonly NumberStyles _numberStyles;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        private delegate (bool, T) TryParseDelegate();

        public abstract bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out T value);
        
        public NumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
        {
            _numberOptions = numberOptions;
            _decimalSeparator = decimalSeparator;
            _groupSeparator = groupSeparator;
            _numberStyles = _numberOptions.ToNumberStyles();

            if (decimalSeparator != CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator[0] ||
                groupSeparator != CultureInfo.InvariantCulture.NumberFormat.NumberGroupSeparator[0])
            {
                _culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                _culture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();
                _culture.NumberFormat.NumberGroupSeparator = groupSeparator.ToString();
            }
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var reset = context.Scanner.Cursor.Position;
            var start = reset.Offset;

            if (context.Scanner.ReadDecimal(_numberOptions, out var number, _decimalSeparator, _groupSeparator))
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
                    context.ReadDecimal(Expression.Constant(_numberOptions), numberSpan, Expression.Constant(_decimalSeparator), Expression.Constant(_groupSeparator)),
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

    internal sealed class ByteNumberLiteral : NumberLiteral<byte>
    {
        public ByteNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out byte value)
        {
            return byte.TryParse(s.ToString(), style, provider, out value );
        }
    }

    internal sealed class SByteNumberLiteral : NumberLiteral<sbyte>
    {
        public SByteNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out sbyte value)
        {
            return sbyte.TryParse(s.ToString(), style, provider, out value);
        }
    }

    internal sealed class IntNumberLiteral : NumberLiteral<int>
    {
        public IntNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out int value)
        {
            return int.TryParse(s.ToString(), style, provider, out value);
        }
    }

    internal sealed class UIntNumberLiteral : NumberLiteral<uint>
    {
        public UIntNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out uint value)
        {
            return uint.TryParse(s.ToString(), style, provider, out value);
        }
    }

    internal sealed class LongNumberLiteral : NumberLiteral<long>
    {
        public LongNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out long value)
        {
            return long.TryParse(s.ToString(), style, provider, out value);
        }
    }

    internal sealed class ULongNumberLiteral : NumberLiteral<ulong>
    {
        public ULongNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ulong value)
        {
            return ulong.TryParse(s.ToString(), style, provider, out value);
        }
    }

    internal sealed class ShortNumberLiteral : NumberLiteral<short>
    {
        public ShortNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out short value)
        {
            return short.TryParse(s.ToString(), style, provider, out value);
        }
    }

    internal sealed class UShortNumberLiteral : NumberLiteral<ushort>
    {
        public UShortNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ushort value)
        {
            return ushort.TryParse(s.ToString(), style, provider, out value);
        }
    }

    internal sealed class DecimalNumberLiteral : NumberLiteral<decimal>
    {
        public DecimalNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out decimal value)
        {
            return decimal.TryParse(s.ToString(), style, provider, out value);
        }
    }

    internal sealed class DoubleNumberLiteral : NumberLiteral<double>
    {
        public DoubleNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out double value)
        {
            return double.TryParse(s.ToString(), style, provider, out value);
        }
    }

    internal sealed class FloatNumberLiteral : NumberLiteral<float>
    {
        public FloatNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out float value)
        {
            return float.TryParse(s.ToString(), style, provider, out value);
        }
    }

#if NET6_0_OR_GREATER
    internal sealed class HalfNumberLiteral : NumberLiteral<Half>
    {
        public HalfNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out Half value)
        {
            return Half.TryParse(s.ToString(), style, provider, out value);
        }
    }
#endif

    internal sealed class BigIntegerNumberLiteral : NumberLiteral<BigInteger>
    {
        public BigIntegerNumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = NumberLiteral.DefaultDecimalSeparator, char groupSeparator = NumberLiteral.DefaultGroupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {

        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out BigInteger value)
        {
            return BigInteger.TryParse(s.ToString(), style, provider, out value);
        }
    }
}
#endif
#endif
