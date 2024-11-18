using System;
using System.Numerics;

namespace Parlot.Fluent;
public static class NumberLiterals
{
    public const char DefaultDecimalSeparator = '.';
    public const char DefaultGroupSeparator = ',';

    public static Parser<T> CreateNumberLiteralParser<T>(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = DefaultDecimalSeparator, char groupSeparator = DefaultGroupSeparator)
#if NET8_0_OR_GREATER
    where T : INumber<T>
    {
        return new NumberLiteral<T>(numberOptions, decimalSeparator, groupSeparator);
#else
    {
        if (typeof(T) == typeof(byte))
        {
            var literal = new ByteNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(sbyte))
        {
            var literal = new SByteNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(int))
        {
            var literal = new IntNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(uint))
        {
            var literal = new UIntNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(long))
        {
            var literal = new LongNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(ulong))
        {
            var literal = new ULongNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(short))
        {
            var literal = new ShortNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(ushort))
        {
            var literal = new UShortNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(decimal))
        {
            var literal = new DecimalNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(double))
        {
            var literal = new DoubleNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else if (typeof(T) == typeof(float))
        {
            var literal = new FloatNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
#if NET6_0_OR_GREATER
        else if (typeof(T) == typeof(Half))
        {
            var literal = new HalfNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
#endif
        else if (typeof(T) == typeof(BigInteger))
        {
            var literal = new BigIntegerNumberLiteral(numberOptions, decimalSeparator, groupSeparator);
            return (literal as NumberLiteralBase<T>)!;
        }
        else
        {
            throw new NotSupportedException($"The type '{typeof(T)}' is not supported as a type argument for '{nameof(NumberLiteralBase<T>)}'. Only numeric types are allowed.");
        }
#endif
    }
}
