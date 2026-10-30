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
    public const string NewLines = "\n\r\v";

    /// <summary>
    /// The chars <see cref="IsWhiteSpace(char)"/> accepts: the ASCII blanks, the no-break space and the Unicode spaces from U+1680 on.
    /// </summary>
    public const string WhiteSpaces = "\t\f \u00A0\u1680\u180E\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u202F\u205F\u3000\uFEFF";

    /// <summary>
    /// The chars <see cref="IsWhiteSpaceOrNewLine(char)"/> accepts: <see cref="WhiteSpaces"/> and <see cref="NewLines"/>.
    /// </summary>
    public const string WhiteSpaceOrNewLines = WhiteSpaces + NewLines;

    /// <summary>
    /// The chars <see cref="IsWhiteSpace(char, WhiteSpaceSet)"/> accepts for <see cref="WhiteSpaceSet.Ascii"/>: tab, form feed and space.
    /// </summary>
    public const string AsciiWhiteSpaces = "\t\f ";

    /// <summary>
    /// The chars of <see cref="WhiteSpaceSet.Ascii"/>: <see cref="AsciiWhiteSpaces"/> and <see cref="NewLines"/>.
    /// </summary>
    public const string AsciiWhiteSpaceOrNewLines = AsciiWhiteSpaces + NewLines;

    // The chars which can start a default identifier, for the parsers to declare as their expected chars
    internal static readonly char[] _identifierStartChars = DefaultIdentifierStart.ToCharArray();
}
