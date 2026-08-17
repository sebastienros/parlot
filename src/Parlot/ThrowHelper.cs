using System;
using System.Runtime.CompilerServices;

namespace Parlot;

internal static class ThrowHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull(object? argument, string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(argument, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative(int argument, string? paramName = null)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(argument, paramName);
#else
        if (argument < 0)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
#endif
    }
}
