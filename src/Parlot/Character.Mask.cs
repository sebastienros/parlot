using System;
#if !NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace Parlot;

[Flags]
internal enum CharacterMask : byte
{
    None = 0,
    IdentifierStart = 1,
    IdentifierPart = 2,
    WhiteSpace = 4,
    WhiteSpaceOrNewLine = 8
}

#if !NET8_0_OR_GREATER
public static partial class Character
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDecimalDigit(char ch) => IsInRange(ch, '0', '9');

    public static bool IsIdentifierStart(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.IdentifierStart) != 0;
    }

    public static bool IsIdentifierPart(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.IdentifierPart) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexDigit(char ch) => HexConverter.IsHexChar(ch);
}
#endif
