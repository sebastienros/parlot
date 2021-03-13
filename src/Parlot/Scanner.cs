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
            bool found = false;
            while (Character.IsWhiteSpace(Cursor.Current))
            {
                Cursor.AdvanceOnce();
                found = true;
            }

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
        public bool ReadChar(char c) => ReadChar(c, out _);

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
        public bool ReadText(string text, StringComparer comparer) => ReadText(text, comparer, out _);

        /// <summary>
        /// Reads the specific expected text.
        /// </summary>
        public bool ReadText(string text, StringComparer comparer, out TokenResult result)
        {
            var match = comparer is null
                ? Cursor.Match(text)
                : Cursor.Match(text, comparer);

            if (!match)
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
        public bool ReadText(string text, out TokenResult result) => ReadText(text, comparer: null, out result);

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

            var nextQuote = Cursor.Buffer.IndexOf(startChar, startOffset);

            if (nextQuote == -1)
            {
                // There is no end quote, not a string
                result = TokenResult.Fail();
                return false;
            }

            var start = Cursor.Position;

            Cursor.Advance();

            var nextEscape = Cursor.Buffer.IndexOf('\\', startOffset, nextQuote - startOffset);

            // If the next escape if not before the next quote, we can return the string as-is
            if (nextEscape == -1 || nextEscape > nextQuote)
            {
                Cursor.Advance(nextQuote + 1 - startOffset);

                result = TokenResult.Succeed(Buffer, start.Offset, Cursor.Offset);
                return true;
            }

            while (!Cursor.Match(startChar))
            {
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

                Cursor.Advance();
            }

            Cursor.Advance();

            result = TokenResult.Succeed(Buffer, start.Offset, Cursor.Offset);

            return true;
        }

        /// <summary>
        /// Reads a sequence token enclosed in arbritrary start and end characters.
        /// </summary>
        /// <remarks>
        /// This method doesn't escape the string, but only validates its content is syntactically correct.
        /// The resulting Span contains the original quotes.
        /// </remarks>
        public bool ReadNonEscapableSequence(char startSequenceChar, char endSequenceChar, out TokenResult result)
        {
            var startChar = Cursor.Current;

            if (startChar != startSequenceChar)
            {
                result = TokenResult.Fail();
                return false;
            }

            // Fast path if there aren't any escape char until next quote
            var startOffset = Cursor.Offset + 1;
            var lastQuote = startOffset;

            int nextQuote ;
            do
            {
                nextQuote = Cursor.Buffer.IndexOf(endSequenceChar, lastQuote + 1);

                if (nextQuote == -1)
                {
                    if(startOffset == lastQuote)
                    {
                        // There is no end sequence character, not a valid escapable sequence
                        result = TokenResult.Fail();
                        return false;
                    }
                    nextQuote = lastQuote - 1;
                    break;
                }

                lastQuote = nextQuote + 1;
            }
            while(Cursor.Buffer.Length > lastQuote && Cursor.Buffer[lastQuote] == endSequenceChar);

            var start = Cursor.Position;

            Cursor.Advance();
            

// If the next escape if not before the next quote, we can return the string as-is
            Cursor.Advance(nextQuote + 1 - startOffset);

            result = TokenResult.Succeed(Buffer, start.Offset, Cursor.Offset);
            return true;
        }
    }
}