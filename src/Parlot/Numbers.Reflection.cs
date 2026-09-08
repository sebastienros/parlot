using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Parlot;

// Runtime grammar construction and the generic delegate path are not emitted into
// standalone consumers, which call the statically dispatched helpers in Numbers.cs.
public static partial class Numbers
{
#if NET8_0_OR_GREATER
    private delegate bool TryParseSpanWithStyles<TNumber>(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out TNumber value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse<TNumber>(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out TNumber value)
        where TNumber : INumber<TNumber>
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

    internal static bool HasTryParseRadixOverload(Type type)
    {
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(BigInteger);
    }

    /// <summary>
    /// Gets the Numbers.TryParse method for a specific numeric type.
    /// </summary>
    internal static MethodInfo GetTryParseMethod(Type type)
    {
        var method = typeof(Numbers).GetMethod(
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
