using System;

namespace Parlot
{
#if !NETSTANDARD2_0
    using System.Diagnostics.CodeAnalysis;
#endif

    internal static class ThrowHelper
    {
#if !NETSTANDARD2_0
        [DoesNotReturn]
#endif
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}
