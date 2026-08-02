#if !NET8_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Parlot;

internal static class ArgumentNullExceptionPolyfills
{
    extension(ArgumentNullException)
    {
        // ArgumentNullException.ThrowIfNull arrived in .NET 6.
        // [NotNull] and [CallerArgumentExpression] are supplied downlevel by PolySharp.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                Throw(paramName);
            }
        }
    }

    [DoesNotReturn]
    private static void Throw(string? paramName) => throw new ArgumentNullException(paramName);
}
#endif
