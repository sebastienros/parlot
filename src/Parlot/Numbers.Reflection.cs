using System;
using System.Globalization;
using System.Numerics;
#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace Parlot;

// Runtime grammar construction and the generic delegate path are not emitted into
// standalone consumers, which call the statically dispatched helpers in Numbers.cs.
public static partial class Numbers
{
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse<TNumber>(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out TNumber value)
        where TNumber : INumber<TNumber>
    {
#pragma warning disable CS8601 // Possible null reference assignment.
        return TNumber.TryParse(s, style, provider, out value);
#pragma warning restore CS8601
    }
#endif

    // The overloads every build of Numbers.TryParse provides, which a downlevel consumer can call
    internal static bool HasTryParseOverload(Type type)
    {
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal)
            || type == typeof(BigInteger);
    }

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
}
