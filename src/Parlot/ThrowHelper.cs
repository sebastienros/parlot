using System;
using System.Diagnostics.CodeAnalysis;

namespace Parlot
{

    internal static class ThrowHelper
    {
        [DoesNotReturn]
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }
        
#if NETSTANDARD2_0
        private class DoesNotReturnAttribute : Attribute {}
#endif
    }
}
