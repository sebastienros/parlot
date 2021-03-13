using System;

namespace Parlot
{
#if SUPPORTS_CODENALYSIS
    using System.Diagnostics.CodeAnalysis;
#endif

    internal static class ThrowHelper
    {
#if SUPPORTS_CODENALYSIS
        [DoesNotReturn]
#endif
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}
