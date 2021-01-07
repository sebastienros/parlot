using System;
using System.Buffers;

namespace Parlot
{
    public class Character
    {
        public static bool IsDecimalDigit(char cp)
        {
            return (cp >= '0' && cp <= '9');
        }

        public static bool IsHexDigit(char cp)
        {
            return (cp >= '0' && cp <= '9') ||
                (cp >= 'A' && cp <= 'F') ||
                (cp >= 'a' && cp <= 'f');
        }

        public static bool IsIdentifierStart(char ch)
        {
            return (ch == '$') || (ch == '_') ||
                   (ch >= 'A' && ch <= 'Z') ||
                   (ch >= 'a' && ch <= 'z');
        }

        public static bool IsIdentifierPart(char ch)
        {
            return (ch == '$') || (ch == '_') ||
                   (ch >= 'A' && ch <= 'Z') ||
                   (ch >= 'a' && ch <= 'z') ||
                   (ch >= '0' && ch <= '9');
        }

        public static bool IsWhiteSpace(char ch)
        {
            return (ch == 32) || // space
                   (ch == '\t') || // horizontal tab
                   (ch == '\v') || // vertical tab
                   (ch == 0xC) || // form feed - new page
                   (ch == 0xA0) || // non-breaking space
                   (ch >= 0x1680 && (
                                        ch == 0x1680 ||
                                        ch == 0x180E ||
                                        (ch >= 0x2000 && ch <= 0x200A) ||
                                        ch == 0x202F ||
                                        ch == 0x205F ||
                                        ch == 0x3000 ||
                                        ch == 0xFEFF));
        }

        public static bool IsWhiteSpaceOrNewLine(char ch)
        {
            return (ch == '\n') || (ch == '\r') || IsWhiteSpace(ch);
        }

        public static char ScanHexEscape(ReadOnlySpan<char> text, int index, out int length)
        {
            var prefix = text[index];
            var len = (prefix == 'u') ? 4 : 2;
            var lastIndex = Math.Min(len + index, text.Length - index);
            var code = 0;

            length = 0;

            for (var i = index + 1; i < lastIndex + 1; i++)
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

        public static ReadOnlySpan<char> DecodeString(ReadOnlySpan<char> buffer)
        {
            // Nothing to do if the string doesn't have any escape char
            if (buffer.IndexOf('\\') == -1)
            {
                return buffer;
            }

            var data = ArrayPool<char>.Shared.Rent(buffer.Length);

            try
            {
                // The asumption is that the new string will be shorter since escapes results are smaller than their source
                //var data = new char[buffer.Length];

                var dataIndex = 0;

                for (var i = 0; i < buffer.Length; i++)
                {
                    var c = buffer[i];

                    if (c == '\\')
                    {
                        i++;
                        c = buffer[i];

                        switch (c)
                        {
                            case '0': data[dataIndex++] = '\0'; break;
                            case '\'': data[dataIndex++] = '\''; break;
                            case '"': data[dataIndex++] = '\"'; break;
                            case '\\': data[dataIndex++] = '\\'; break;
                            case 'b': data[dataIndex++] = '\b'; break;
                            case 'f': data[dataIndex++] = '\f'; break;
                            case 'n': data[dataIndex++] = '\n'; break;
                            case 'r': data[dataIndex++] = '\r'; break;
                            case 't': data[dataIndex++] = '\t'; break;
                            case 'v': data[dataIndex++] = '\v'; break;
                            case 'u':
                                data[dataIndex++] = Character.ScanHexEscape(buffer, i, out var length);
                                i += length;
                                break;
                            case 'x':
                                data[dataIndex++] = Character.ScanHexEscape(buffer, i, out length);
                                i += length;
                                break;
                        }
                    }
                    else
                    {
                        data[dataIndex++] = c;
                    }
                }

                return String.Create(dataIndex, data, (chars, source) =>
                {
                    source.AsSpan(0, chars.Length).CopyTo(chars);
                }).AsSpan();
            }
            finally
            {
                ArrayPool<char>.Shared.Return(data);
            }
        }

        private static int HexValue(char ch)
        {
            if (ch >= '0' && ch <= '9')
            {
                return ch - 48;
            }
            else if (ch >= 'a' && ch <= 'f')
            {
                return ch - 'a' + 10;
            }
            else if (ch >= 'A' && ch <= 'F')
            {
                return ch - 'A' + 10;
            }
            else
            {
                return 0;
            }
        }
    }
}
