using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Parlot;

/// <summary>
/// Centralized numeric parsing helpers used by number literals and source generation.
/// </summary>
public static class Numbers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out byte value)
    {
        return byte.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out sbyte value)
    {
        return sbyte.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out short value)
    {
        return short.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ushort value)
    {
        return ushort.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out int value)
    {
        return int.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out uint value)
    {
        return uint.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out long value)
    {
        return long.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ulong value)
    {
        return ulong.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out float value)
    {
        return float.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out double value)
    {
        return double.TryParse(s, style, provider, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out decimal value)
    {
        return decimal.TryParse(s, style, provider, out value);
    }

// System.Half arrived in .NET 5; net472 and netstandard2.0 lack the type, so this overload
// cannot exist there at all. A missing type is not polyfillable, unlike a missing member.
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out Half value)
    {
        return Half.TryParse(s, style, provider, out value);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out BigInteger value)
    {
        return BigInteger.TryParse(s, style, provider, out value);
    }

// INumber<T> arrived in .NET 7; net472 and netstandard2.0 lack the type entirely,
// so this whole generic lane is modern-only and cannot be polyfilled.
#if NET8_0_OR_GREATER
        /// <summary>
        /// Parses a number, using a fast path for plain sequences of digits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseNumber<TNumber>(ReadOnlySpan<char> span, NumberStyles styles, IFormatProvider provider, [MaybeNullWhen(false)] out TNumber value)
                where TNumber : INumber<TNumber>
        {
                if ((uint)(span.Length - 1) < MaxFastDigits)
                {
                        long parsed = 0;

                        foreach (var c in span)
                        {
                                var digit = (uint)(c - '0');

                                if (digit > 9)
                                {
                                        return TNumber.TryParse(span, styles, provider, out value);
                                }

                                parsed = (parsed * 10) + digit;
                        }

                        value = TNumber.CreateTruncating(parsed);

                        // A narrower number type can wrap during conversion, so only keep exact values.
                        if (long.CreateTruncating(value) == parsed)
                        {
                                return true;
                        }
                }

                return TNumber.TryParse(span, styles, provider, out value);
        }

        // long.MaxValue has 19 digits, so every number with at most 18 digits fits.
        private const int MaxFastDigits = 18;

        private delegate bool TryParseSpanWithStyles<TNumber>(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out TNumber value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse<TNumber>(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out TNumber value)
                where TNumber : System.Numerics.INumber<TNumber>
        {
                // Prefer a TryParse overload that accepts NumberStyles if available to honor number options.
                var withStyles = TryParseDelegate<TNumber>.WithStyles;
                if (withStyles is not null)
                {
                        return withStyles(s, style, provider, out value);
                }

                // Fallback to INumberBase.TryParse(ReadOnlySpan<char>, IFormatProvider?, out TNumber)
        #pragma warning disable CS8601 // Possible null reference assignment.
                return TNumber.TryParse(s, provider, out value);
        #pragma warning restore CS8601
        }

        private static class TryParseDelegate<TNumber>
        {
                public static readonly TryParseSpanWithStyles<TNumber>? WithStyles = CreateWithStyles();

                private static TryParseSpanWithStyles<TNumber>? CreateWithStyles()
                {
                        var method = typeof(TNumber).GetMethod(
                                "TryParse",
                                BindingFlags.Public | BindingFlags.Static,
                                binder: null,
                                types: new[] { typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(TNumber).MakeByRefType() },
                                modifiers: null);

                        return method is null
                                ? null
                                : method.CreateDelegate<TryParseSpanWithStyles<TNumber>>();
                }
        }
#endif

        /// <summary>
        /// Gets the Numbers.TryParse method for a specific numeric type.
        /// </summary>
        internal static MethodInfo GetTryParseMethod(Type type)
        {
                MethodInfo? method = typeof(Numbers).GetMethod(
                        nameof(TryParse),
                        BindingFlags.Public | BindingFlags.Static,
                        binder: null,
                        types: new[] { typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), type.MakeByRefType() },
                        modifiers: null);

#if NET8_0_OR_GREATER
                if (method is null && ImplementsINumber(type))
                {
                        method = _genericTryParseMethod?.MakeGenericMethod(type);
                }
#endif

                return method ?? throw new NotSupportedException($"Numbers.TryParse is not available for type '{type}'.");
        }

        internal static MethodInfo GetTryParseMethod<T>()
                => GetTryParseMethod(typeof(T));

#if NET8_0_OR_GREATER
        private static readonly MethodInfo? _genericTryParseMethod = typeof(Numbers)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.IsGenericMethodDefinition && m.Name == nameof(TryParse) && m.GetParameters().Length == 4);

        private static bool ImplementsINumber(Type type)
                => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumber<>));
#endif
}
