#if NET8_0_OR_GREATER
using System.Buffers;

namespace Parlot.Benchmarks;
internal class SearchValuesHelper
{
    internal const string DecimalDigits = "0123456789";
    internal const string Alpha = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    internal const string AlphaNumeric = Alpha + DecimalDigits;
    internal const string DefaultIdentifierStart = "$_" + Alpha;
    internal const string DefaultIdentifierPart = "$_" + AlphaNumeric;
    internal const string HexDigits = "0123456789abcdefABCDEF";
    internal const string WhiteSpacesAscii = " \t\f\xa0";
    internal const string WhiteSpacesNonAscii = "\x1680\x180E\x2000\x2001\x2002\x2003\x2004\x2005\x2006\x2007\x2008\x2009\x200a\x202f\x205f\x3000\xfeff";

    internal const string NewLines = "\n\r\v";

    internal static readonly SearchValues<char> _decimalDigits = SearchValues.Create(DecimalDigits);
    internal static readonly SearchValues<char> _hexDigits = SearchValues.Create(HexDigits);
    internal static readonly SearchValues<char> _identifierStart = SearchValues.Create(DefaultIdentifierStart);
    internal static readonly SearchValues<char> _identifierPart = SearchValues.Create(DefaultIdentifierPart);
    internal static readonly SearchValues<char> _whiteSpacesAscii = SearchValues.Create(WhiteSpacesAscii);
    internal static readonly SearchValues<char> _whiteSpacesNonAscii = SearchValues.Create(WhiteSpacesNonAscii);
    internal static readonly SearchValues<char> _whiteSpaces = SearchValues.Create(WhiteSpacesAscii + WhiteSpacesNonAscii);
    internal static readonly SearchValues<char> _newLines = SearchValues.Create(NewLines);
    internal static readonly SearchValues<char> _whiteSpaceOrNewLines = SearchValues.Create(WhiteSpacesAscii + WhiteSpacesNonAscii + NewLines);
    internal static readonly SearchValues<char> _whiteSpaceOrNewLinesAscii = SearchValues.Create(WhiteSpacesAscii + NewLines);
}
#endif
