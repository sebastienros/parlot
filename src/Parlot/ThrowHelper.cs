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
}
