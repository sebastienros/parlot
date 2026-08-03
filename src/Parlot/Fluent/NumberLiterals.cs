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
        // No Half branch: this is the !NET8_0_OR_GREATER leg, and System.Half does not exist on
        // net472 or netstandard2.0. On net8.0+ the INumber<T> lane above handles Half instead.
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

    public static Parser<T> CreateRadixNumberLiteralParser<T>(int radix)
#if NET8_0_OR_GREATER
    where T : IBinaryInteger<T>
#endif
    {
        if (typeof(T) == typeof(byte))
        {
            return (Parser<T>)(object)new RadixNumberLiteral<byte>(radix, Numbers.TryParseRadix);
        }
        else if (typeof(T) == typeof(sbyte))
        {
            return (Parser<T>)(object)new RadixNumberLiteral<sbyte>(radix, Numbers.TryParseRadix);
        }
        else if (typeof(T) == typeof(short))
        {
            return (Parser<T>)(object)new RadixNumberLiteral<short>(radix, Numbers.TryParseRadix);
        }
        else if (typeof(T) == typeof(ushort))
        {
            return (Parser<T>)(object)new RadixNumberLiteral<ushort>(radix, Numbers.TryParseRadix);
        }
        else if (typeof(T) == typeof(int))
        {
            return (Parser<T>)(object)new RadixNumberLiteral<int>(radix, Numbers.TryParseRadix);
        }
        else if (typeof(T) == typeof(uint))
        {
            return (Parser<T>)(object)new RadixNumberLiteral<uint>(radix, Numbers.TryParseRadix);
        }
        else if (typeof(T) == typeof(long))
        {
            return (Parser<T>)(object)new RadixNumberLiteral<long>(radix, Numbers.TryParseRadix);
        }
        else if (typeof(T) == typeof(ulong))
        {
            return (Parser<T>)(object)new RadixNumberLiteral<ulong>(radix, Numbers.TryParseRadix);
        }
        else if (typeof(T) == typeof(BigInteger))
        {
            return (Parser<T>)(object)new RadixNumberLiteral<BigInteger>(radix, Numbers.TryParseRadix);
        }
#if NET8_0_OR_GREATER
        else
        {
            return new RadixNumberLiteral<T>(radix, Numbers.TryParseRadix<T>);
        }
#else
        else
        {
            throw new NotSupportedException($"The type '{typeof(T)}' is not supported as a radix number type.");
        }
#endif
    }
}
