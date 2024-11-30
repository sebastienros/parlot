#if NET8_0_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Parlot;

public static partial class Character
{
    public const string DecimalDigits = "0123456789";
    public const string Alpha = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string AlphaNumeric = Alpha + DecimalDigits;
    public const string DefaultIdentifierStart = "$_" + Alpha;
    public const string DefaultIdentifierPart = "$_" + AlphaNumeric;
    public const string HexDigits = "0123456789abcdefABCDEF";
    public const string NewLines = "\n\r\v";

    internal static readonly SearchValues<char> _decimalDigits = SearchValues.Create(DecimalDigits);
    internal static readonly SearchValues<char> _hexDigits = SearchValues.Create(HexDigits);
    internal static readonly SearchValues<char> _identifierStart = SearchValues.Create(DefaultIdentifierStart);
    internal static readonly SearchValues<char> _identifierPart = SearchValues.Create(DefaultIdentifierPart);
    internal static readonly SearchValues<char> _newLines = SearchValues.Create(NewLines);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDecimalDigit(char ch) => _decimalDigits.Contains(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIdentifierStart(char ch) => _identifierStart.Contains(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIdentifierPart(char ch) => _identifierPart.Contains(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexDigit(char ch) => _hexDigits.Contains(ch);
}
#endif
