using System;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Parlot
{
    internal static class ThrowHelper
    {
#if NET6_0_OR_GREATER
        [DoesNotReturn]
#endif
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}
