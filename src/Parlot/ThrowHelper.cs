using System;

namespace Parlot;

internal static class ThrowHelper
{
    public static void ThrowIfNull(object? argument, string? paramName = null)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(argument, paramName);
#else
        if (argument is null)
        {
#pragma warning disable CA1510
            throw new ArgumentNullException(paramName);
#pragma warning restore CA1510
        }
#endif
    }
}
