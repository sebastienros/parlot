#if !NET8_0_OR_GREATER
using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Parlot;

/// <summary>
/// Backfills the span-based <c>TryParse</c> overloads on the numeric primitives so that call sites can be
/// written as if the project only targeted the newest .NET. On the frameworks that have the real API the
/// BCL member wins overload resolution, so this type only ever binds downlevel.
/// </summary>
/// <remarks>
/// Static extension members lower into the container with the receiver type erased from the signature, which
/// normally forces one container per receiver type to avoid CS0111. It does not apply here: every overload
/// below differs in the type of its <c>out</c> parameter, so they remain distinct after erasure and a single
/// container is correct.
/// </remarks>
internal static class NumberPolyfills
{
    // All the overloads below arrived in .NET Core 2.1 / netstandard2.1.

    extension(byte)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out byte result)
            => byte.TryParse(s.ToString(), style, provider, out result);
    }

    extension(sbyte)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out sbyte result)
            => sbyte.TryParse(s.ToString(), style, provider, out result);
    }

    extension(short)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out short result)
            => short.TryParse(s.ToString(), style, provider, out result);
    }

    extension(ushort)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out ushort result)
            => ushort.TryParse(s.ToString(), style, provider, out result);
    }

    extension(int)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out int result)
            => int.TryParse(s.ToString(), style, provider, out result);
    }

    extension(uint)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out uint result)
            => uint.TryParse(s.ToString(), style, provider, out result);
    }

    extension(long)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out long result)
            => long.TryParse(s.ToString(), style, provider, out result);
    }

    extension(ulong)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out ulong result)
            => ulong.TryParse(s.ToString(), style, provider, out result);
    }

    extension(float)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out float result)
            => float.TryParse(s.ToString(), style, provider, out result);
    }

    extension(double)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out double result)
            => double.TryParse(s.ToString(), style, provider, out result);
    }

    extension(decimal)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out decimal result)
            => decimal.TryParse(s.ToString(), style, provider, out result);
    }

    extension(BigInteger)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> value, NumberStyles style, IFormatProvider? provider, out BigInteger result)
            => BigInteger.TryParse(value.ToString(), style, provider, out result);
    }
}
#endif
