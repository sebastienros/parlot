using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Parlot;

public static partial class Character
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRange(char ch, char min, char max) => ch - (uint)min <= max - (uint)min;

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

    public static string DecodeStringInternal(ReadOnlySpan<char> span)
    {
        // This method always allocates a new string. It is invoked when we know for sure that the string contains escape sequences.

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
                var c = span[i];

                if (c == '\\')
                {
                    i++;
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
                            c = Character.ScanHexEscape(span[i..], out var length);
                            i += length;
                            break;
                        case 'x':
                            c = Character.ScanHexEscape(span[i..], out length);
                            i += length;
                            break;
                    }
                }

                buffer[dataIndex++] = c;
            }

            return buffer[..dataIndex].ToString();
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

    public static bool IsWhiteSpace(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.WhiteSpace) != 0;
    }

    public static bool IsWhiteSpaceOrNewLine(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.WhiteSpaceOrNewLine) != 0;
    }

    public static bool IsNewLine(char ch) => ch is '\n' or '\r' or '\v';
}
