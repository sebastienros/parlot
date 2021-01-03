using System;

namespace Parlot
{
    using System.Diagnostics.CodeAnalysis;

    internal static class ThrowHelper
    {
        [DoesNotReturn]
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}
