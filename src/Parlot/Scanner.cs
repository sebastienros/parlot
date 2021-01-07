using System;

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

        /// <summary>
        /// Scans some text.
        /// </summary>
        /// <param name="buffer">The string containing the text to scan.</param>
        public Scanner(string buffer)
        {
            Buffer = buffer;
            Cursor = new Cursor(Buffer, TextPosition.Start);
        }

        /// <summary>
        /// Reads any whitespace without generating a token.
        /// </summary>
        /// <returns>Whether some white space was read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SkipWhiteSpaceOrNewLine()
        {
            if (!Character.IsWhiteSpaceOrNewLine(Cursor.Current))
            {
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

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SkipWhiteSpace()
        {
            if (!Character.IsWhiteSpace(Cursor.Current))
            {
                return false;
            }

            return SkipWhiteSpaceUnlikely();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool SkipWhiteSpaceUnlikely()
        {
            Cursor.Advance();

            while (Character.IsWhiteSpace(Cursor.Current))
            {
                Cursor.Advance();
            }

            return true;
        }

        public bool ReadFirstThenOthers(Func<char, bool> first, Func<char, bool> other, ITokenResult result = null)
        {
            if (!first(Cursor.Current))
            {
                result?.Fail();
                return false;
            }

            var start = Cursor.Offset;

            // At this point we have an identifier, read while it's an identifier part.

            Cursor.Advance();

            ReadWhile(other, null);

            result?.Succeed(Buffer, start, Cursor.Offset);

            return true;
        }

        public bool ReadIdentifier(ITokenResult result = null)
        {
            // perf: using Character.IsIdentifierStart instead of x => Character.IsIdentifierStart(x) induces some allocations

            return ReadFirstThenOthers(static x => Character.IsIdentifierStart(x), static x => Character.IsIdentifierPart(x), result);
        }

        public bool ReadDecimal(ITokenResult result = null)
        {
            // perf: fast path to prevent a copy of the position

            if (!Character.IsDecimalDigit(Cursor.Current))
            {
                result?.Fail();
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
                    result?.Fail();
                    Cursor.ResetPosition(start);
                    return false;
                }

                do
                {
                    Cursor.Advance();

                } while (!Cursor.Eof && Character.IsDecimalDigit(Cursor.Current));
            }

            result?.Succeed(Buffer, start.Offset, Cursor.Offset);
            return true;
        }

        public bool ReadInteger(ITokenResult result = null)
        {
            // perf: fast path to prevent a copy of the position

            if (!Character.IsDecimalDigit(Cursor.Current))
            {
                result?.Fail();
                return false;
            }

            var start = Cursor.Offset;

            do
            {
                Cursor.Advance();

            } while (!Cursor.Eof && Character.IsDecimalDigit(Cursor.Current));

            result?.Succeed(Buffer, start, Cursor.Offset);
            return true;
        }

        /// <summary>
        /// Reads a token while the specific predicate is valid.
        /// </summary>
        public bool ReadWhile(Func<char, bool> predicate, ITokenResult result = null)
        {
            if (Cursor.Eof || !predicate(Cursor.Current))
            {
                result?.Fail();
                return false;
            }

            var start = Cursor.Offset;

            Cursor.Advance();

            while (!Cursor.Eof && predicate(Cursor.Current))
            {
                Cursor.Advance();
            }

            result?.Succeed(Buffer, start, Cursor.Offset);

            return true;
        }

        public bool ReadNonWhiteSpace(ITokenResult result = null)
        {
            return ReadWhile(static x => !Character.IsWhiteSpace(x), result);
        }

        /// <summary>
        /// Reads the specified text.
        /// </summary>
        public bool ReadChar(char c, ITokenResult result = null)
        {
            if (!Cursor.Match(c))
            {
                result?.Fail();
                return false;
            }

            if (result != null)
            {
                var start = Cursor.Offset;

                Cursor.Advance();

                result?.Succeed(Buffer, start, Cursor.Offset);
            }
            else
            {
                Cursor.Advance();
            }

            return true;
        }

        /// <summary>
        /// Reads the specific expected text.
        /// </summary>
        public bool ReadText(string text, StringComparer comparer = null, ITokenResult result = null)
        {
            // Default comparison is ordinal.
            // Use implementation of Match() that doesn't use any comparer in this case.
            if (comparer == null)
            {
                if (!Cursor.Match(text))
                {
                    result?.Fail();
                    return false;
                }
            }
            else
            {
                if (!Cursor.Match(text, comparer))
                {
                    result?.Fail();
                    return false;
                }
            }

            var start = 0;

            // perf: don't allocate a new TextPosition if we don't need to return it
            if (result != null)
            {
                start = Cursor.Offset;
            }

            Cursor.Advance(text.Length);

            result?.Succeed(Buffer, start, Cursor.Offset);
            
            return true;
        }

        public bool ReadSingleQuotedString(ITokenResult result = null)
        {
            return ReadQuotedString('\'', result);
        }

        public bool ReadDoubleQuotedString(ITokenResult result = null)
        {
            return ReadQuotedString('\"', result);
        }

        public bool ReadQuotedString(ITokenResult result = null)
        {
            var startChar = Cursor.Current;

            if (startChar != '\'' && startChar != '\"')
            {
                result?.Fail();
                return false;
            }

            return ReadQuotedString(startChar, result);
        }

        /// <summary>
        /// Reads a string token enclosed in single or double quotes.
        /// </summary>
        /// <remarks>
        /// This method doesn't escape the string, but only validates its content is syntactically correct.
        /// The resulting Span contains the original quotes.
        /// </remarks>
        private bool ReadQuotedString(char quoteChar, ITokenResult result = null)
        {
            var startChar = Cursor.Current;

            if (startChar != quoteChar)
            {
                result?.Fail();
                return false;
            }

            // Fast path if there aren't any escape char until next quote
            var startOffset = Cursor.Offset + 1;

            var nextQuote = Cursor.Buffer.IndexOf(startChar, startOffset);

            if (nextQuote == -1)
            {
                // There is no end quote, not a string
                result?.Fail();
                return false;
            }

            var start = Cursor.Position;

            Cursor.Advance();

            var nextEscape = Cursor.Buffer.IndexOf('\\', startOffset, nextQuote - startOffset);

            // If the next escape if not before the next quote, we can return the string as-is
            if (nextEscape == -1 || nextEscape > nextQuote)
            {
                Cursor.Advance(nextQuote + 1 - startOffset);

                result?.Succeed(Buffer, start.Offset, Cursor.Offset);
                return true;
            }

            while (!Cursor.Match(startChar))
            {
                // We can read Eof if there is an escaped quote sequence and no actual end quote, e.g. "'abc\'def"
                if (Cursor.Eof)
                {
                    result?.Fail();
                    return false;
                }

                if (Cursor.Match('\\'))
                {
                    Cursor.Advance();

                    switch (Cursor.Current)
                    {
                        case '0':
                        case '\'':
                        case '"':
                        case '\\':
                        case 'b':
                        case 'f':
                        case 'n':
                        case 'r':
                        case 't':
                        case 'v':
                            break;
                        case 'u':
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
                                        isValidUnicode = true;
                                    }
                                }
                            }

                            if (!isValidUnicode)
                            {
                                Cursor.ResetPosition(start);

                                result?.Fail();
                                return false;
                            }

                            break;
                        case 'x':
                            bool isValidHex = false;

                            Cursor.Advance();

                            if (!Cursor.Eof && Character.IsHexDigit(Cursor.Current))
                            {
                                Cursor.Advance();
                                if (!Cursor.Eof && Character.IsHexDigit(Cursor.Current))
                                {
                                    isValidHex = true;
                                }
                            }

                            if (!isValidHex)
                            {
                                Cursor.ResetPosition(start);

                                result?.Fail();
                                return false;
                            }

                            break;
                        default:
                            Cursor.ResetPosition(start);

                            result?.Fail();
                            return false;
                    }
                }

                Cursor.Advance();
            }

            Cursor.Advance();

            result?.Succeed(Buffer, start.Offset, Cursor.Offset);

            return true;
        }
    }
}
