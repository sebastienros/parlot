#if NET8_0_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Parlot;

public static partial class Character
{
    internal static readonly SearchValues<char> _decimalDigits = SearchValues.Create(DecimalDigits);
    internal static readonly SearchValues<char> _hexDigits = SearchValues.Create(HexDigits);
    internal static readonly SearchValues<char> _identifierStart = SearchValues.Create(DefaultIdentifierStart);
    internal static readonly SearchValues<char> _identifierPart = SearchValues.Create(DefaultIdentifierPart);
    internal static readonly SearchValues<char> _newLines = SearchValues.Create(NewLines);
    internal static readonly SearchValues<char> _whiteSpaces = SearchValues.Create(WhiteSpaces);
    internal static readonly SearchValues<char> _whiteSpaceOrNewLines = SearchValues.Create(WhiteSpaceOrNewLines);
    internal static readonly SearchValues<char> _asciiWhiteSpaces = SearchValues.Create(AsciiWhiteSpaces);
    internal static readonly SearchValues<char> _asciiWhiteSpaceOrNewLines = SearchValues.Create(AsciiWhiteSpaceOrNewLines);
    internal static readonly SearchValues<char> _jsonWhiteSpaces = SearchValues.Create(JsonWhiteSpaces);
    internal static readonly SearchValues<char> _jsonWhiteSpaceOrNewLines = SearchValues.Create(JsonWhiteSpaceOrNewLines);

    // The chars of a white space mask, see GetWhiteSpaceMask
    internal static SearchValues<char> GetWhiteSpaceSearchValues(byte mask) => mask switch
    {
        (byte)CharacterMask.WhiteSpace => _whiteSpaces,
        (byte)CharacterMask.WhiteSpaceOrNewLine => _whiteSpaceOrNewLines,
        (byte)CharacterMask.AsciiWhiteSpace => _asciiWhiteSpaces,
        (byte)CharacterMask.AsciiWhiteSpaceOrNewLine => _asciiWhiteSpaceOrNewLines,
        (byte)CharacterMask.JsonWhiteSpace => _jsonWhiteSpaces,
        _ => _jsonWhiteSpaceOrNewLines,
    };

    // _decimalDigits and _hexDigits are still used for span-wide IndexOfAnyExcept scans in Scanner,
    // which is what SearchValues is good at. The single-char predicates live in Character.cs and use
    // the BCL's char.IsAscii* instead -- see the comment there.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIdentifierStart(char ch) => _identifierStart.Contains(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIdentifierPart(char ch) => _identifierPart.Contains(ch);
}
#endif
