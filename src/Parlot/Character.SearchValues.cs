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

    private const string WhiteSpaceCharacters = " \t\f\u00a0\u1680\u180e\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a\u202f\u205f\u3000\ufeff";
    internal static readonly SearchValues<char> _whiteSpaces = SearchValues.Create(WhiteSpaceCharacters);
    internal static readonly SearchValues<char> _whiteSpaceOrNewLines = SearchValues.Create(WhiteSpaceCharacters + "\n\r\v");

    // _decimalDigits and _hexDigits are still used for span-wide IndexOfAnyExcept scans in Scanner,
    // which is what SearchValues is good at. The single-char predicates live in Character.cs and use
    // the BCL's char.IsAscii* instead -- see the comment there.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIdentifierStart(char ch) => _identifierStart.Contains(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIdentifierPart(char ch) => _identifierPart.Contains(ch);
}
#endif
