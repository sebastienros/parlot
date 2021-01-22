using System;

namespace Parlot
{
    public static class Character
    {
        public static bool IsDecimalDigit(char ch)
            => ch >= '0' && ch <= '9';

        public static bool IsHexDigit(char ch)
            => IsDecimalDigit(ch) ||
                (ch >= 'A' && ch <= 'F') ||
                (ch >= 'a' && ch <= 'f');

        public static bool IsIdentifierStart(char ch)
            => (ch == '$') || (ch == '_') ||
               (ch >= 'A' && ch <= 'Z') ||
               (ch >= 'a' && ch <= 'z');

        public static bool IsIdentifierPart(char ch)
            => IsIdentifierStart(ch) || IsDecimalDigit(ch);

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
            => (ch == '\n') || (ch == '\r') || IsWhiteSpace(ch);

        public static char ScanHexEscape(string text, int index, out int length)
        {
            var lastIndex = Math.Min(4 + index, text.Length);
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

        public static TextSpan DecodeString(string s) => DecodeString(new TextSpan(s));

        public static TextSpan DecodeString(TextSpan span)
        {
            // Nothing to do if the string doesn't have any escape char
            if (span.Buffer.IndexOf('\\', span.Offset, span.Length) == -1)
            {
                return span;
            }

#if NETSTANDARD2_0
            var result = CreateString(span.Length, span, static (chars, source) =>
#else
            var result = String.Create(span.Length, span, static (chars, source) =>
#endif
            {
                // The asumption is that the new string will be shorter since escapes results are smaller than their source

                var dataIndex = 0;
                var buffer = source.Buffer;
                var start = source.Offset;
                var end = source.Offset + source.Length;

                for (var i = start; i < end; i++)
                {
                    var c = buffer[i];

                    if (c == '\\')
                    {
                        i++;
                        c = buffer[i];

                        switch (c)
                        {
                            case '0': c = '\0'; break;
                            case '\'': c = '\''; break;
                            case '"': c = '\"'; break;
                            case '\\': c = '\\'; break;
                            case 'b': c = '\b'; break;
                            case 'f': c = '\f'; break;
                            case 'n': c = '\n'; break;
                            case 'r': c = '\r'; break;
                            case 't': c = '\t'; break;
                            case 'v': c = '\v'; break;
                            case 'u':
                                c = Character.ScanHexEscape(buffer, i, out var length);
                                i += length;
                                break;
                            case 'x':
                                c = Character.ScanHexEscape(buffer, i, out length);
                                i += length;
                                break;
                        }
                    }

                    chars[dataIndex++] = c;
                }

                chars[dataIndex++] = '\0';
            });

            for (var i = result.Length - 1; i >= 0; i--)
            {
                if (result[i] != '\0')
                {
                    return new TextSpan(result, 0, i + 1);
                }
            }

            return new TextSpan(result);
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

#if NETSTANDARD2_0
        private delegate void SpanAction<T, in TArg>(T[] span, TArg arg);
        private static string CreateString<TState>(int length, TState state, SpanAction<char, TState> action)
        {
            var array = new char[length];

            action(array, state);

            return new string(array);
        }
#endif
    }
}
