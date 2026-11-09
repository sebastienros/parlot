using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Parlot.Fluent;

namespace Parlot;

public static partial class Character
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRange(char ch, char min, char max) => ch - (uint)min <= max - (uint)min;

    // DecimalDigits and HexDigits are exactly the ASCII digit and ASCII hex-digit sets, so the BCL
    // predicates are equivalent and compile to arithmetic instead of a SearchValues probe. See
    // CharacterSetsBenchmarks: 0.19 ns -> 0.02 ns for digits, 0.12-0.21 ns -> ~0 ns for hex digits.
    // IsIdentifierStart/IsIdentifierPart have no such BCL equivalent and keep using SearchValues,
    // which the same benchmark shows is the fastest option for those sets.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDecimalDigit(char ch) => char.IsAsciiDigit(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexDigit(char ch) => char.IsAsciiHexDigit(ch);

    public static char ScanHexEscape(string text, int index, out int length)
    {
        return ScanHexEscape(text.AsSpan(index), out length);
    }

    public static char ScanHexEscape(ReadOnlySpan<char> text, out int length)
    {
        var lastIndex = Math.Min(4, text.Length - 1);
        var code = 0;

        length = 0;

        for (var i = 1; i < lastIndex + 1; i++)
        {
            var d = text[i];

            if (!IsHexDigit(d))
            {
                break;
            }

            length++;
            code = code * 16 + HexValue(d);
        }

        return (char)code;
    }

    [Obsolete("Use DecodeString(TextSpan) or DecodeString(string) instead for performance reasons.")]
    public static ReadOnlySpan<char> DecodeString(ReadOnlySpan<char> span)
    {
        // Use other overloads for better performance as they can return the original string/span when no escape char is present.
        return DecodeStringInternal(span).AsSpan();
    }

    public static string DecodeStringInternal(ReadOnlySpan<char> span) => DecodeStringInternal(span, pool: null);

    /// <summary>
    /// Decodes a string with escape sequences, taking the result from <paramref name="pool"/> when there is one.
    /// </summary>
    internal static string DecodeStringInternal(ReadOnlySpan<char> span, StringPool? pool)
    {
        // This method allocates a new string, unless the pool has it. It is invoked when we know for sure that the string contains escape sequences.

        // The assumption is that the new string will be shorter since escapes results are smaller than their source
        char[]? rentedBuffer = null;
        Span<char> buffer = span.Length <= 128
            ? stackalloc char[span.Length]
            : (rentedBuffer = ArrayPool<char>.Shared.Rent(span.Length));

        try
        {
            var dataIndex = 0;

            for (var i = 0; i < span.Length; i++)
            {
#if NET8_0_OR_GREATER
                if (span.Length - i >= 16)
                {
                    var remaining = span.Slice(i);
                    var nextEscape = remaining.IndexOf('\\');
                    var count = nextEscape < 0 ? remaining.Length : nextEscape;
                    if (count > 0)
                    {
                        remaining.Slice(0, count).CopyTo(buffer.Slice(dataIndex));
                        dataIndex += count;
                        if (nextEscape < 0)
                        {
                            break;
                        }
                        i += count;
                    }
                }
#endif
                var c = span[i];

                if (c == '\\')
                {
                    i++;

                    // A backslash at the end doesn't escape anything, it is kept as-is
                    if (i == span.Length)
                    {
                        buffer[dataIndex++] = '\\';
                        break;
                    }

                    c = span[i];

                    switch (c)
                    {
                        case '\'': c = '\''; break;
                        case '"': c = '\"'; break;
                        case '\\': c = '\\'; break;
                        case '0': c = '\0'; break;
                        case 'a': c = '\a'; break;
                        case 'b': c = '\b'; break;
                        case 'f': c = '\f'; break;
                        case 'n': c = '\n'; break;
                        case 'r': c = '\r'; break;
                        case 't': c = '\t'; break;
                        case 'v': c = '\v'; break;
                        case 'u':
                            c = ScanHexEscape(span.Slice(i), out var length);
                            i += length;
                            break;
                        case 'x':
                            c = ScanHexEscape(span.Slice(i), out length);
                            i += length;
                            break;
                    }
                }

                buffer[dataIndex++] = c;
            }

            if (pool != null)
            {
                return pool.Get(buffer.Slice(0, dataIndex));
            }

            // Escape sequences often decode to a single char, like "\n"
            return dataIndex == 1 && buffer[0] < 128 ? StringPool.Ascii(buffer[0]) : buffer.Slice(0, dataIndex).ToString();
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    // The flags that make a backslash start an escape sequence
    internal const StringEscapes BackslashEscapes = (StringEscapes)((int)StringEscapes.LineContinuation * 2 - 1);

    // The values ReadEscape returns for a sequence that isn't a char
    internal const int RemovedEscape = -1;
    internal const int KeptEscape = -2;

    /// <summary>
    /// Reads the escape sequence that follows a backslash, for escapes other than <see cref="StringEscapes.Default"/>.
    /// </summary>
    /// <param name="span">The text after the backslash, not empty.</param>
    /// <param name="escapes">The escape sequences to accept.</param>
    /// <param name="quote">The quote that delimits the string.</param>
    /// <param name="value">The code point, <see cref="RemovedEscape"/> for a line continuation, or <see cref="KeptEscape"/>
    /// when the backslash and the sequence are kept as is.</param>
    /// <returns>The length of the sequence after the backslash; 0 when it is invalid, -1 when it is invalid because
    /// <paramref name="span"/> ends before it does.</returns>
    internal static int ReadEscape(ReadOnlySpan<char> span, StringEscapes escapes, char quote, out int value)
    {
        var c = span[0];

        // A line terminator is a continuation before it is an escaped quote, so that the decoder, which doesn't have
        // the cursor, reads the same sequences as the scanner
        if ((escapes & StringEscapes.LineContinuation) != 0 && c is '\n' or '\r' or '\u2028' or '\u2029')
        {
            value = RemovedEscape;
            return c == '\r' && span.Length > 1 && span[1] == '\n' ? 2 : 1;
        }

        if ((escapes & StringEscapes.Octal) != 0 && IsInRange(c, '0', '7'))
        {
            // \0 to \377: up to three digits from \0 to \3, two from \4 to \7. The digits after the first one are
            // optional, an escape that reaches the end of the span ends there.
            var octal = c - '0';
            var length = 1;
            var maxLength = c <= '3' ? 3 : 2;

            while (length < maxLength && length < span.Length && IsInRange(span[length], '0', '7'))
            {
                octal = octal * 8 + (span[length] - '0');
                length++;
            }

            value = octal;
            return length;
        }

        value = c switch
        {
            '\\' => '\\',
            'n' when (escapes & StringEscapes.NewLineAndTab) != 0 => '\n',
            'r' when (escapes & StringEscapes.NewLineAndTab) != 0 => '\r',
            't' when (escapes & StringEscapes.NewLineAndTab) != 0 => '\t',
            'b' when (escapes & StringEscapes.BackspaceAndFormFeed) != 0 => '\b',
            'f' when (escapes & StringEscapes.BackspaceAndFormFeed) != 0 => '\f',
            'v' when (escapes & StringEscapes.VerticalTab) != 0 => '\v',
            'a' when (escapes & StringEscapes.Alert) != 0 => '\a',
            '0' when (escapes & StringEscapes.Null) != 0 => '\0',
            'e' when (escapes & StringEscapes.Escape) != 0 => '\u001b',
            '/' when (escapes & StringEscapes.Slash) != 0 => '/',
            '?' when (escapes & StringEscapes.QuestionMark) != 0 => '?',
            's' when (escapes & StringEscapes.Space) != 0 => ' ',
            '\'' or '"' when (escapes & StringEscapes.Quotes) != 0 => c,
            _ => -1,
        };

        if (value >= 0)
        {
            return 1;
        }

        switch (c)
        {
            case 'u' when (escapes & StringEscapes.BracedUnicode) != 0 && (span.Length == 1 || span[1] == '{'):
                return ReadBracedHex(span, out value);
            case 'u' when (escapes & StringEscapes.Unicode) != 0:
                return ReadHex(span, 4, 4, out value);
            case 'U' when (escapes & StringEscapes.LongUnicode) != 0:
                var longLength = ReadHex(span, 8, 8, out value);
                // Eight digits can overflow the value
                return value is < 0 or > 0x10FFFF ? 0 : longLength;
            case 'x' when (escapes & StringEscapes.TwoDigitHex) != 0:
                return ReadHex(span, 2, 2, out value);
            case 'x' when (escapes & StringEscapes.Hex) != 0:
                return ReadHex(span, 1, 4, out value);
        }

        // The chars no flag claims
        if (c == quote && (escapes & StringEscapes.EscapedQuote) != 0)
        {
            value = c;
            return 1;
        }

        if ((escapes & StringEscapes.KeepUnknown) != 0)
        {
            value = KeptEscape;
            return 1;
        }

        if ((escapes & StringEscapes.AnyChar) != 0)
        {
            value = c;
            return 1;
        }

        value = 0;
        return 0;
    }

    // Reads the hex digits after the char at span[0]
    private static int ReadHex(ReadOnlySpan<char> span, int minDigits, int maxDigits, out int value)
    {
        value = 0;
        var digits = 0;

        while (digits < maxDigits && digits + 1 < span.Length && IsHexDigit(span[digits + 1]))
        {
            value = value * 16 + HexValue(span[digits + 1]);
            digits++;
        }

        if (digits < minDigits)
        {
            // The digits that are there run to the end of the span
            return digits + 1 == span.Length ? -1 : 0;
        }

        return digits + 1;
    }

    // Reads "{", the hex digits of a code point up to U+10FFFF and "}" after the char at span[0]
    private static int ReadBracedHex(ReadOnlySpan<char> span, out int value)
    {
        value = 0;

        if (span.Length == 1)
        {
            return -1;
        }

        var i = 2;

        while (i < span.Length && IsHexDigit(span[i]))
        {
            value = value * 16 + HexValue(span[i]);

            if (value > 0x10FFFF)
            {
                return 0;
            }

            i++;
        }

        if (i == span.Length)
        {
            return -1;
        }

        return i == 2 || span[i] != '}' ? 0 : i + 1;
    }

    /// <summary>
    /// Decodes a string with the escape sequences of <paramref name="escapes"/>, delimited by <paramref name="quote"/>,
    /// taking the result from <paramref name="pool"/> when there is one. The scanner validated it with the same escapes.
    /// </summary>
    internal static string DecodeStringInternal(ReadOnlySpan<char> span, StringEscapes escapes, char quote, StringPool? pool)
    {
        if (escapes == StringEscapes.Default)
        {
            return DecodeStringInternal(span, pool);
        }

        // No escape sequence is longer once decoded: a code point above U+FFFF has at least six chars, and a kept
        // sequence its own length
        char[]? rentedBuffer = null;
        Span<char> buffer = span.Length <= 128
            ? stackalloc char[span.Length]
            : (rentedBuffer = ArrayPool<char>.Shared.Rent(span.Length));

        try
        {
            var escapeChar = (escapes & BackslashEscapes) != 0 ? '\\' : quote;
            var nextChar = (escapes & StringEscapes.DoubledQuote) != 0 ? quote : escapeChar;
            var dataIndex = 0;
            var i = 0;

            while (i < span.Length)
            {
                var next = span.Slice(i).IndexOfAny(escapeChar, nextChar);
                var count = next < 0 ? span.Length - i : next;

                span.Slice(i, count).CopyTo(buffer.Slice(dataIndex));
                dataIndex += count;
                i += count;

                if (next < 0)
                {
                    break;
                }

                if (span[i] == quote && i + 1 < span.Length && span[i + 1] == quote && (escapes & StringEscapes.DoubledQuote) != 0)
                {
                    buffer[dataIndex++] = quote;
                    i += 2;
                    continue;
                }

                // A backslash at the end, or that doesn't start a valid sequence, is kept as is
                var value = 0;
                var length = span[i] == '\\' && i + 1 < span.Length ? ReadEscape(span.Slice(i + 1), escapes, quote, out value) : 0;

                if (length <= 0)
                {
                    buffer[dataIndex++] = span[i++];
                    continue;
                }

                if (value == KeptEscape)
                {
                    buffer[dataIndex++] = '\\';
                    buffer[dataIndex++] = span[i + 1];
                }
                else if (value > 0xFFFF)
                {
                    value -= 0x10000;
                    buffer[dataIndex++] = (char)(0xD800 + (value >> 10));
                    buffer[dataIndex++] = (char)(0xDC00 + (value & 0x3FF));
                }
                else if (value != RemovedEscape)
                {
                    buffer[dataIndex++] = (char)value;
                }

                i += length + 1;
            }

            if (pool != null)
            {
                return pool.Get(buffer.Slice(0, dataIndex));
            }

            return dataIndex == 1 && buffer[0] < 128 ? StringPool.Ascii(buffer[0]) : buffer.Slice(0, dataIndex).ToString();
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    public static string DecodeString(string s)
    {
        if (string.IsNullOrEmpty(s) || s.AsSpan().IndexOf('\\') == -1)
        {
            return s;
        }

        return DecodeStringInternal(s.AsSpan());
    }

    public static TextSpan DecodeString(TextSpan textSpan)
    {
        if (textSpan.Span.IsEmpty || textSpan.Span.IndexOf('\\') == -1)
        {
            return textSpan;
        }

        return new TextSpan(DecodeStringInternal(textSpan.Span));
    }

    public static TextSpan DecodeString(string s, int offset, int length)
    {
        var span = s.AsSpan().Slice(offset, length);

        if (span.IsEmpty || span.IndexOf('\\') == -1)
        {
            return new TextSpan(s, offset, length);
        }

        return new TextSpan(DecodeStringInternal(span));
    }

    private static int HexValue(char ch) => HexConverter.FromChar(ch);

    /// <summary>
    /// Whether a char is white space, but not a new line, in the default set: one of <see cref="WhiteSpaces"/>.
    /// </summary>
    public static bool IsWhiteSpace(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.WhiteSpace) != 0;
    }

    /// <summary>
    /// Whether a char is white space or a new line in the default set: one of <see cref="WhiteSpaceOrNewLines"/>, the chars
    /// <see cref="char.IsWhiteSpace(char)"/> accepts.
    /// </summary>
    public static bool IsWhiteSpaceOrNewLine(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.WhiteSpaceOrNewLine) != 0;
    }

    /// <summary>
    /// Whether a char is white space, but not a new line, in a given set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhiteSpace(char ch, WhiteSpaceSet set)
    {
        return (_characterData[ch] & GetWhiteSpaceMask(set)) != 0;
    }

    /// <summary>
    /// Whether a char is white space or a new line in a given set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhiteSpaceOrNewLine(char ch, WhiteSpaceSet set)
    {
        return (_characterData[ch] & GetWhiteSpaceOrNewLineMask(set)) != 0;
    }

    // The bit of a set's white space in _characterData. The bit of its white space or new lines is the next one.
    internal static byte GetWhiteSpaceMask(WhiteSpaceSet set) => set switch
    {
        WhiteSpaceSet.Default => (byte)CharacterMask.WhiteSpace,
        WhiteSpaceSet.Ascii => (byte)CharacterMask.AsciiWhiteSpace,
        WhiteSpaceSet.SpaceTabCrLf => (byte)CharacterMask.SpaceTabCrLfWhiteSpace,
        _ => ThrowInvalidWhiteSpaceSet(set),
    };

    internal static byte GetWhiteSpaceOrNewLineMask(WhiteSpaceSet set) => (byte)(GetWhiteSpaceMask(set) << 1);

    // Whether a char has one of the bits of a mask in _characterData
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasMask(char ch, byte mask) => (_characterData[ch] & mask) != 0;

    private static byte ThrowInvalidWhiteSpaceSet(WhiteSpaceSet set)
        => throw new ArgumentOutOfRangeException(nameof(set), set, "Unknown white space set.");

    /// <summary>
    /// Whether a char is a new line of the default set: one of <see cref="NewLines"/>.
    /// </summary>
    public static bool IsNewLine(char ch) => ch is '\n' or '\r' or '\v' or '\u0085' or '\u2028' or '\u2029';
}
