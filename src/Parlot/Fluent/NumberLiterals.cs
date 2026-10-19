using System;
using System.Globalization;
using System.Numerics;
using System.Reflection;

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
        else if (SpanNumberLiteral<T>.TryParseMethod is not null)
        {
            // Types this build can't name, such as Half, Int128, UInt128, nint or nuint, when the runtime
            // provides their span TryParse. This is how the netstandard2.0 build runs inside the source
            // generator host, which executes on a modern runtime and emits the consumer's generic path.
            return new SpanNumberLiteral<T>(numberOptions, decimalSeparator, groupSeparator);
        }
        else
        {
            throw new NotSupportedException($"The type '{typeof(T)}' is not supported as a type argument for '{nameof(NumberLiteralBase<T>)}'. Only numeric types are allowed.");
        }
#endif
    }

#if !NET8_0_OR_GREATER
    private sealed class SpanNumberLiteral<T> : NumberLiteralBase<T>
    {
        internal delegate bool TryParseSpan(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out T value);

        // T.TryParse(ReadOnlySpan<char>, NumberStyles, IFormatProvider, out T), resolved once per type
        public static readonly TryParseSpan? TryParseMethod = CreateTryParse();

        public SpanNumberLiteral(NumberOptions numberOptions, char decimalSeparator, char groupSeparator)
            : base(numberOptions, decimalSeparator, groupSeparator)
        {
        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out T value)
            => TryParseMethod!(s, style, provider, out value);

        private static TryParseSpan? CreateTryParse()
        {
            var method = typeof(T).GetMethod(
                "TryParse",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(T).MakeByRefType()],
                modifiers: null);

            return method is null || method.ReturnType != typeof(bool)
                ? null
                : (TryParseSpan)Delegate.CreateDelegate(typeof(TryParseSpan), method);
        }
    }

    /// <summary>
    /// Whether <typeparamref name="T"/> implements the generic math interface the net8.0+ radix parser requires,
    /// which this build can't reference. True on a modern runtime, such as the source generator host.
    /// </summary>
    private static bool IsBinaryInteger<T>()
    {
        foreach (var @interface in typeof(T).GetInterfaces())
        {
            if (@interface.IsGenericType && @interface.GetGenericTypeDefinition().FullName == "System.Numerics.IBinaryInteger`1")
            {
                return true;
            }
        }

        return false;
    }

    private static bool ThrowTryParseRadix<T>(ReadOnlySpan<char> span, int radix, out int charsRead, out T value)
    {
        throw new NotSupportedException($"Parsing radix numbers of type '{typeof(T)}' requires the .NET 8 or later build of Parlot.");
    }
#endif

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
        else if (IsBinaryInteger<T>())
        {
            // The source generator host runs this build on a modern runtime and emits the consumer's generic
            // path; the delegate is only reached when such a parser is executed here.
            return new RadixNumberLiteral<T>(radix, ThrowTryParseRadix<T>);
        }
        else
        {
            throw new NotSupportedException($"The type '{typeof(T)}' is not supported as a radix number type.");
        }
#endif
    }
}
