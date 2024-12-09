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
}
