using System;
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
#if NET6_0_OR_GREATER
        return byte.TryParse(s, style, provider, out value);
#else
        return byte.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out sbyte value)
    {
#if NET6_0_OR_GREATER
        return sbyte.TryParse(s, style, provider, out value);
#else
        return sbyte.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out short value)
    {
#if NET6_0_OR_GREATER
        return short.TryParse(s, style, provider, out value);
#else
        return short.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ushort value)
    {
#if NET6_0_OR_GREATER
        return ushort.TryParse(s, style, provider, out value);
#else
        return ushort.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out int value)
    {
#if NET6_0_OR_GREATER
        return int.TryParse(s, style, provider, out value);
#else
        return int.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out uint value)
    {
#if NET6_0_OR_GREATER
        return uint.TryParse(s, style, provider, out value);
#else
        return uint.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out long value)
    {
#if NET6_0_OR_GREATER
        return long.TryParse(s, style, provider, out value);
#else
        return long.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out ulong value)
    {
#if NET6_0_OR_GREATER
        return ulong.TryParse(s, style, provider, out value);
#else
        return ulong.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out float value)
    {
#if NET6_0_OR_GREATER
        return float.TryParse(s, style, provider, out value);
#else
        return float.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out double value)
    {
#if NET6_0_OR_GREATER
        return double.TryParse(s, style, provider, out value);
#else
        return double.TryParse(s.ToString(), style, provider, out value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out decimal value)
    {
#if NET6_0_OR_GREATER
        return decimal.TryParse(s, style, provider, out value);
#else
        return decimal.TryParse(s.ToString(), style, provider, out value);
#endif
    }

#if NET6_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out Half value)
    {
        return Half.TryParse(s, style, provider, out value);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out BigInteger value)
    {
#if NET6_0_OR_GREATER
        return BigInteger.TryParse(s, style, provider, out value);
#else
        return BigInteger.TryParse(s.ToString(), style, provider, out value);
#endif
    }

#if NET7_0_OR_GREATER // INumber<T> arrives in net7
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

#if NET7_0_OR_GREATER
                if (method is null && ImplementsINumber(type))
                {
                        method = _genericTryParseMethod?.MakeGenericMethod(type);
                }
#endif

                return method ?? throw new NotSupportedException($"Numbers.TryParse is not available for type '{type}'.");
        }

        internal static MethodInfo GetTryParseMethod<T>()
                => GetTryParseMethod(typeof(T));

#if NET7_0_OR_GREATER
        private static readonly MethodInfo? _genericTryParseMethod = typeof(Numbers)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.IsGenericMethodDefinition && m.Name == nameof(TryParse) && m.GetParameters().Length == 4);

        private static bool ImplementsINumber(Type type)
                => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumber<>));
#endif
}
