using System;
using System.Runtime.CompilerServices;

namespace Parlot
{
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
        public static bool IsDecimalDigit(char ch) => IsInRange(ch, '0', '9');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRange(char ch, char min, char max) => ch - (uint) min <= max - (uint) min;

        public static bool IsHexDigit(char ch) => HexConverter.IsHexChar(ch);

        public static bool IsIdentifierStart(char ch)
        {
            return (_characterData[ch] & (byte) CharacterMask.IdentifierStart) != 0;
        }

        public static bool IsIdentifierPart(char ch)
        {
            return (_characterData[ch] & (byte) CharacterMask.IdentifierPart) != 0;
        }

        public static bool IsWhiteSpace(char ch)
        {
            return (_characterData[ch] & (byte) CharacterMask.WhiteSpace) != 0;
        }

        public static bool IsWhiteSpaceNonAscii(char ch)
        {
            return (ch >= 0x1680 && (
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
            return (_characterData[ch] & (byte) CharacterMask.WhiteSpaceOrNewLine) != 0;
        }

        public static bool IsNewLine(char ch) => ch is '\n' or '\r' or '\v';

        public static char ScanHexEscape(string text, int index, out int length)
        {
            var lastIndex = Math.Min(4 + index, text.Length - 1);
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
            if (span.Buffer.AsSpan(span.Offset, span.Length).IndexOf('\\') == -1)
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

        private static int HexValue(char ch) => HexConverter.FromChar(ch);

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
