namespace Parlot;

public static partial class Character
{
    public const string DecimalDigits = "0123456789";
    public const string HexDigits = "0123456789abcdefABCDEF";
    public const string OctalDigits = "01234567";
    public const string BinaryDigits = "01";

    public const string AZLower = "abcdefghijklmnopqrstuvwxyz";
    public const string AZUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string AZ = AZLower + AZUpper;
    public const string AlphaNumeric = AZ + DecimalDigits;
    public const string DefaultIdentifierStart = "$_" + AZ;
    public const string DefaultIdentifierPart = "$_" + AZ + DecimalDigits;

    /// <summary>
    /// The new lines of <see cref="WhiteSpaceSet.Default"/>, which <see cref="IsNewLine(char)"/> accepts: <c>\n</c>, <c>\r</c>, <c>\v</c>,
    /// the next line char U+0085, and the line and paragraph separators U+2028 and U+2029.
    /// </summary>
    public const string NewLines = "\n\r\v\u0085\u2028\u2029";

    /// <summary>
    /// The chars <see cref="IsWhiteSpace(char)"/> accepts: tab, form feed, space, the no-break space U+00A0 and the other Unicode
    /// spaces, U+1680, U+2000-U+200A, U+202F, U+205F and U+3000.
    /// </summary>
    public const string WhiteSpaces = "\t\f \u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u202F\u205F\u3000";

    /// <summary>
    /// The chars <see cref="IsWhiteSpaceOrNewLine(char)"/> accepts: <see cref="WhiteSpaces"/> and <see cref="NewLines"/>.
    /// These are the chars of the Unicode <c>White_Space</c> property, the ones <see cref="char.IsWhiteSpace(char)"/> and the
    /// <c>\s</c> of .NET regular expressions accept.
    /// </summary>
    public const string WhiteSpaceOrNewLines = WhiteSpaces + NewLines;

    /// <summary>
    /// The chars <see cref="IsWhiteSpace(char, WhiteSpaceSet)"/> accepts for <see cref="WhiteSpaceSet.Ascii"/>: tab, form feed and space.
    /// </summary>
    public const string AsciiWhiteSpaces = "\t\f ";

    /// <summary>
    /// The new lines of <see cref="WhiteSpaceSet.Ascii"/>: <c>\n</c>, <c>\r</c> and <c>\v</c>.
    /// </summary>
    public const string AsciiNewLines = "\n\r\v";

    /// <summary>
    /// The chars of <see cref="WhiteSpaceSet.Ascii"/>: <see cref="AsciiWhiteSpaces"/> and <see cref="AsciiNewLines"/>.
    /// </summary>
    public const string AsciiWhiteSpaceOrNewLines = AsciiWhiteSpaces + AsciiNewLines;

    /// <summary>
    /// The chars <see cref="IsWhiteSpace(char, WhiteSpaceSet)"/> accepts for <see cref="WhiteSpaceSet.SpaceTabCrLf"/>: tab and space.
    /// </summary>
    public const string SpaceTabCrLfWhiteSpaces = "\t ";

    /// <summary>
    /// The new lines of <see cref="WhiteSpaceSet.SpaceTabCrLf"/>: <c>\n</c> and <c>\r</c>.
    /// </summary>
    public const string SpaceTabCrLfNewLines = "\n\r";

    /// <summary>
    /// The chars of <see cref="WhiteSpaceSet.SpaceTabCrLf"/>: <see cref="SpaceTabCrLfWhiteSpaces"/> and <see cref="SpaceTabCrLfNewLines"/>.
    /// </summary>
    public const string SpaceTabCrLfWhiteSpaceOrNewLines = SpaceTabCrLfWhiteSpaces + SpaceTabCrLfNewLines;

    // The chars which can start a default identifier, for the parsers to declare as their expected chars
    internal static readonly char[] _identifierStartChars = DefaultIdentifierStart.ToCharArray();
}
