﻿using System;

namespace Parlot
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// This class is used to return tokens extracted from the input buffer.
    /// </summary>
    public class Scanner
    {
        public readonly string Buffer;
        public readonly Cursor Cursor;

        private readonly record struct WhiteSpaceMarker(int Offset, bool IsNotWhiteSpace, bool IsNotWhiteSpaceOrNewLine);

        // Caches the latest whitespace check. Remember that the current position is not a whitespace.
        private WhiteSpaceMarker _whiteSpaceMarker = new (-1, false, false);

        /// <summary>
        /// Scans some text.
        /// </summary>
        /// <param name="buffer">The string containing the text to scan.</param>
        public Scanner(string buffer)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Cursor = new Cursor(Buffer, TextPosition.Start);
        }

        /// <summary>
        /// Reads any whitespace without generating a token.
        /// </summary>
        /// <returns>Whether some white space was read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SkipWhiteSpaceOrNewLine()
        {
            // Don't read if we already know it's not a whitespace
            if (Cursor.Position.Offset == _whiteSpaceMarker.Offset && _whiteSpaceMarker.IsNotWhiteSpaceOrNewLine)
            {
                return false;
            }

            if (!Character.IsWhiteSpaceOrNewLine(Cursor.Current))
            {
                // Memorize the fact that the current offset is not a whitespace
                _whiteSpaceMarker = new (Cursor.Position.Offset, true, true);

                return false;
            }

            return SkipWhiteSpaceOrNewLineUnlikely();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool SkipWhiteSpaceOrNewLineUnlikely()
        {
            Cursor.Advance();

            while (Character.IsWhiteSpaceOrNewLine(Cursor.Current))
            {
                Cursor.Advance();
            }

            // Memorize the fact that the current offset is not a whitespace or new line
            _whiteSpaceMarker = new (Cursor.Position.Offset, true, true);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SkipWhiteSpace()
        {
            // Don't read if we already know it's not a whitespace
            if (Cursor.Position.Offset == _whiteSpaceMarker.Offset && _whiteSpaceMarker.IsNotWhiteSpace)
            {
                return false;
            }

            bool found = false;
            while (Character.IsWhiteSpace(Cursor.Current))
            {
                Cursor.Advance();
                found = true;
            }

            // Memorize the fact that the current offset is not a whitespace
            _whiteSpaceMarker = new (Cursor.Position.Offset, true, false);

            return found;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadFirstThenOthers(Func<char, bool> first, Func<char, bool> other)
            => ReadFirstThenOthers(first, other, out _); 

        public bool ReadFirstThenOthers(Func<char, bool> first, Func<char, bool> other, out TokenResult result)
        {
            if (!first(Cursor.Current))
            {
                result = TokenResult.Fail();
                return false;
            }

            var start = Cursor.Offset;

            // At this point we have an identifier, read while it's an identifier part.

            Cursor.Advance();

            ReadWhile(other, out _);

            result = TokenResult.Succeed(Buffer, start, Cursor.Offset);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadIdentifier() => ReadIdentifier(out _);

        public bool ReadIdentifier(out TokenResult result)
        {
            // perf: using Character.IsIdentifierStart instead of x => Character.IsIdentifierStart(x) induces some allocations

            return ReadFirstThenOthers(static x => Character.IsIdentifierStart(x), static x => Character.IsIdentifierPart(x), out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadDecimal() => ReadDecimal(out _);

        public bool ReadDecimal(out TokenResult result)
        {
            // perf: fast path to prevent a copy of the position

            if (!Character.IsDecimalDigit(Cursor.Current))
            {
                result = TokenResult.Fail();
                return false;
            }

            var start = Cursor.Position;

            do
            {
                Cursor.Advance();

            } while (!Cursor.Eof && Character.IsDecimalDigit(Cursor.Current));

            if (Cursor.Match('.'))
            {
                Cursor.Advance();

                if (!Character.IsDecimalDigit(Cursor.Current))
                {
                    result = TokenResult.Fail();
                    Cursor.ResetPosition(start);
                    return false;
                }

                do
                {
                    Cursor.Advance();

                } while (!Cursor.Eof && Character.IsDecimalDigit(Cursor.Current));
            }

            result = TokenResult.Succeed(Buffer, start.Offset, Cursor.Offset);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadInteger() => ReadInteger(out _);
        
        public bool ReadInteger(out TokenResult result)
        {
            // perf: fast path to prevent a copy of the position

            if (!Character.IsDecimalDigit(Cursor.Current))
            {
                result = TokenResult.Fail();
                return false;
            }

            var start = Cursor.Offset;

            do
            {
                Cursor.Advance();

            } while (!Cursor.Eof && Character.IsDecimalDigit(Cursor.Current));

            result = TokenResult.Succeed(Buffer, start, Cursor.Offset);
            return true;
        }

        /// <summary>
        /// Reads a token while the specific predicate is valid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadWhile(Func<char, bool> predicate) => ReadWhile(predicate, out _);

        /// <summary>
        /// Reads a token while the specific predicate is valid.
        /// </summary>
        public bool ReadWhile(Func<char, bool> predicate, out TokenResult result)
        {
            if (Cursor.Eof || !predicate(Cursor.Current))
            {
                result = TokenResult.Fail();
                return false;
            }

            var start = Cursor.Offset;

            Cursor.Advance();

            while (!Cursor.Eof && predicate(Cursor.Current))
            {
                Cursor.Advance();
            }

            result = TokenResult.Succeed(Buffer, start, Cursor.Offset);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadNonWhiteSpace() => ReadNonWhiteSpace(out _); 

        public bool ReadNonWhiteSpace(out TokenResult result)
        {
            return ReadWhile(static x => !Character.IsWhiteSpace(x), out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadNonWhiteSpaceOrNewLine() => ReadNonWhiteSpaceOrNewLine(out _); 

        public bool ReadNonWhiteSpaceOrNewLine(out TokenResult result)
        {
            return ReadWhile(static x => !Character.IsWhiteSpaceOrNewLine(x), out result);
        }

        /// <summary>
        /// Reads the specified text.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadChar(char c)
        {
            if (!Cursor.Match(c))
            {
                return false;
            }

            Cursor.Advance();
            return true;
        }

        /// <summary>
        /// Reads the specified text.
        /// </summary>
        public bool ReadChar(char c, out TokenResult result)
        {
            if (!Cursor.Match(c))
            {
                result = TokenResult.Fail();
                return false;
            }

            var start = Cursor.Offset;
            Cursor.Advance();

            result = TokenResult.Succeed(Buffer, start, Cursor.Offset);
            return true;
        }

        /// <summary>
        /// Reads the specific expected text.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadText(string text, StringComparison comparisonType) => ReadText(text, comparisonType, out _); 
        
        /// <summary>
        /// Reads the specific expected text.
        /// </summary>
        public bool ReadText(string text, StringComparison comparisonType, out TokenResult result)
        {
            if (!Cursor.Match(text, comparisonType))
            {
                result = TokenResult.Fail();
                return false;
            }

            int start = Cursor.Offset;
            Cursor.Advance(text.Length);
            result = TokenResult.Succeed(Buffer, start, Cursor.Offset);
            
            return true;
        }

        /// <summary>
        /// Reads the specific expected text.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadText(string text) => ReadText(text, out _);

        /// <summary>
        /// Reads the specific expected text.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadText(string text, out TokenResult result) => ReadText(text, comparisonType: StringComparison.Ordinal, out result);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadSingleQuotedString() => ReadSingleQuotedString(out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadSingleQuotedString(out TokenResult result)
        {
            return ReadQuotedString('\'', out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadDoubleQuotedString() => ReadDoubleQuotedString(out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadDoubleQuotedString(out TokenResult result)
        {
            return ReadQuotedString('\"', out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadQuotedString() => ReadQuotedString(out _);

        public bool ReadQuotedString(out TokenResult result)
        {
            var startChar = Cursor.Current;

            if (startChar != '\'' && startChar != '\"')
            {
                result = TokenResult.Fail();
                return false;
            }

            return ReadQuotedString(startChar, out result);
        }

        /// <summary>
        /// Reads a string token enclosed in single or double quotes.
        /// </summary>
        /// <remarks>
        /// This method doesn't escape the string, but only validates its content is syntactically correct.
        /// The resulting Span contains the original quotes.
        /// </remarks>
        private bool ReadQuotedString(char quoteChar, out TokenResult result)
        {
            var startChar = Cursor.Current;

            if (startChar != quoteChar)
            {
                result = TokenResult.Fail();
                return false;
            }

            // Fast path if there aren't any escape char until next quote
            var startOffset = Cursor.Offset + 1;

            var nextQuote = Cursor.Buffer.AsSpan(startOffset).IndexOf(startChar);

            if (nextQuote == -1)
            {
                // There is no end quote, not a string
                result = TokenResult.Fail();
                return false;
            }

            var start = Cursor.Position;

            Cursor.Advance();

            var nextEscape = Cursor.Buffer.AsSpan(startOffset, nextQuote).IndexOf('\\');

            // If the next escape if not before the next quote, we can return the string as-is
            if (nextEscape == -1)
            {
                Cursor.Advance(nextQuote + 1);

                result = TokenResult.Succeed(Buffer, start.Offset, Cursor.Offset);
                return true;
            }

            while (nextEscape != -1)
            {
                Cursor.Advance(nextEscape);

                // We can read Eof if there is an escaped quote sequence and no actual end quote, e.g. "'abc\'def"
                if (Cursor.Eof)
                {
                    result = TokenResult.Fail();
                    return false;
                }

                if (Cursor.Match('\\'))
                {
                    Cursor.Advance();

                    switch (Cursor.Current)
                    {
                        case '0':
                        case '\\':
                        case 'b':
                        case 'f':
                        case 'n':
                        case 'r':
                        case 't':
                        case 'v':
                        case '\'':
                        case '"':
                            Cursor.Advance();
                            break;

                        case 'u':

                            // https://stackoverflow.com/a/32175520/142772
                            // exactly 4 digits

                            var isValidUnicode = false;

                            Cursor.Advance();

                            if (!Cursor.Eof && Character.IsHexDigit(Cursor.Current))
                            {
                                Cursor.Advance();
                                if (!Cursor.Eof && Character.IsHexDigit(Cursor.Current))
                                {
                                    Cursor.Advance();
                                    if (!Cursor.Eof && Character.IsHexDigit(Cursor.Current))
                                    {
                                        Cursor.Advance();
                                        if (!Cursor.Eof && Character.IsHexDigit(Cursor.Current))
                                        {
                                            isValidUnicode = true;
                                        }
                                    }
                                }
                            }

                            if (!isValidUnicode)
                            {
                                Cursor.ResetPosition(start);

                                result = TokenResult.Fail();
                                return false;
                            }

                            break;
                        case 'x':

                            // https://stackoverflow.com/a/32175520/142772
                            // exactly 4 digits

                            bool isValidHex = false;

                            Cursor.Advance();

                            if (!Cursor.Eof && Character.IsHexDigit(Cursor.Current))
                            {
                                isValidHex = true;

                                if (!Cursor.Eof && Character.IsHexDigit(Cursor.PeekNext()))
                                {
                                    Cursor.Advance();

                                    if (!Cursor.Eof && Character.IsHexDigit(Cursor.PeekNext()))
                                    {
                                        Cursor.Advance();

                                        if (!Cursor.Eof && Character.IsHexDigit(Cursor.PeekNext()))
                                        {
                                            Cursor.Advance();
                                        }
                                    }
                                }
                            }

                            if (!isValidHex)
                            {
                                Cursor.ResetPosition(start);

                                result = TokenResult.Fail();
                                return false;
                            }

                            break;
                        default:
                            Cursor.ResetPosition(start);

                            result = TokenResult.Fail();
                            return false;
                    }
                }

                nextEscape = Cursor.Buffer.AsSpan(Cursor.Offset).IndexOfAny('\\', startChar);

                if (Cursor.Match(startChar))
                {
                    Cursor.Advance(nextEscape + 1);
                    break;
                }
                else if (nextEscape == -1)
                {
                    Cursor.ResetPosition(start);

                    result = TokenResult.Fail();
                    return false;
                }
            }            

            result = TokenResult.Succeed(Buffer, start.Offset, Cursor.Offset);

            return true;
        }
    }
}
