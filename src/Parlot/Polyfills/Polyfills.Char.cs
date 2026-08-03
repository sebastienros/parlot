#if !NET8_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace Parlot;

internal static class CharPolyfills
{
    extension(char)
    {
        // char.IsAsciiDigit arrived in .NET 7.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiDigit(char c) => Character.IsInRange(c, '0', '9');

        // char.IsAsciiHexDigit arrived in .NET 7. HexConverter.IsHexChar is the branchless
        // shift-and-mask the BCL itself uses, vendored into this repository already.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiHexDigit(char c) => HexConverter.IsHexChar(c);
    }
}
#endif
