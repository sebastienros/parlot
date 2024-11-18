using System;
using System.Buffers;
using System.Runtime.CompilerServices;

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

public static partial class Character
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDecimalDigit(char ch) => IsInRange(ch, '0', '9');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRange(char ch, char min, char max) => ch - (uint)min <= max - (uint)min;

    public static bool IsHexDigit(char ch) => HexConverter.IsHexChar(ch);

    public static bool IsIdentifierStart(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.IdentifierStart) != 0;
    }

    public static bool IsIdentifierPart(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.IdentifierPart) != 0;
    }

    public static bool IsWhiteSpace(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.WhiteSpace) != 0;
    }

    public static bool IsWhiteSpaceOrNewLine(char ch)
    {
        return (_characterData[ch] & (byte)CharacterMask.WhiteSpaceOrNewLine) != 0;
    }

    public static bool IsNewLine(char ch) => ch is '\n' or '\r' or '\v';

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

    public static ReadOnlySpan<char> DecodeString(ReadOnlySpan<char> span)
    {
        // Nothing to do if the string doesn't have any escape char
        if (span.IsEmpty || span.IndexOf('\\') == -1)
        {
            return span;
        }

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

            var result = buffer[..dataIndex].ToString().AsSpan();

            return result;
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    public static TextSpan DecodeString(string s) => DecodeString(new TextSpan(s));

    public static TextSpan DecodeString(TextSpan span)
    {
        // Nothing to do if the string doesn't have any escape char
        if (string.IsNullOrEmpty(span.Buffer) || span.Buffer.AsSpan(span.Offset, span.Length).IndexOf('\\') == -1)
        {
            return span;
        }

        return new TextSpan(DecodeString(span.Span).ToString());

    }

    private static int HexValue(char ch) => HexConverter.FromChar(ch);
}
