using System;

namespace Parlot
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>
    /// Calling a Read() method without a token type won't emit a token. This can be used to reuse Read() method without other 
    /// Read() methods without trigger a new Token for each sub-call.
    /// </remarks>
    public class Scanner<T>
    {
        public static readonly Token<T> EmptyToken = Token<T>.Empty;

        public readonly string Buffer;
        public Cursor Cursor;       

        public Scanner(string buffer)
        {
            Buffer = buffer;
            Cursor = new Cursor(Buffer, TextPosition.Start);
        }

        public Func<Token<T>, Token<T>> OnToken { get; set; }

        /// <summary>
        /// Reads any whitespace without generating a token.
        /// </summary>
        /// <returns>Whether some white space was read.</returns>
        public bool SkipWhiteSpace()
        {
            if (!Character.IsWhiteSpace(Cursor.Peek()))
            {
                return false;
            }

            Cursor.Advance();

            while (Character.IsWhiteSpace(Cursor.Peek()))
            {
                Cursor.Advance();
            }

            return true;
        }

        public bool ReadFirstThenOthers(Func<char, bool> first, Func<char, bool> other, TokenResult<T> result = null, T tokenType = default)
        {
            if (!first(Cursor.Peek()))
            {
                return false;
            }

            var start = Cursor.Position;

            // At this point we have an identifier, read while it's an identifier part.

            Cursor.Advance();

            ReadWhile(other, null);

            result?.SetToken(tokenType, Buffer, start, Cursor.Position);

            return true;
        }

        public bool ReadIdentifier(TokenResult<T> result = null, T tokenType = default)
        {
            // perf: using Character.IsIdentifierStart instead of x => Character.IsIdentifierStart(x) induces some allocations

            return ReadFirstThenOthers(static x => Character.IsIdentifierStart(x), static x => Character.IsIdentifierPart(x), result, tokenType);
        }

        public bool ReadDecimal(TokenResult<T> result = null, T tokenType = default)
        {
            

            // perf: fast path to prevent a copy of the position

            if (!Char.IsDigit(Cursor.Peek()))
            {
                return false;
            }

            var start = Cursor.Position;

            do
            {
                Cursor.Advance();

            } while (!Cursor.Eof && Char.IsDigit(Cursor.Peek()));

            if (Cursor.Match('.'))
            {
                Cursor.Advance();

                if (!Char.IsDigit(Cursor.Peek()))
                {
                    Cursor.ResetPosition(start);
                    return false;
                }

                do
                {
                    Cursor.Advance();

                } while (!Cursor.Eof && Char.IsDigit(Cursor.Peek()));
            }

            result?.SetToken(tokenType, Buffer, start, Cursor.Position);
            return true;
        }

        /// <summary>
        /// Reads a token while the specific predicate is valid.
        /// </summary>
        public bool ReadWhile(Func<char, bool> predicate, TokenResult<T> result = null, T tokenType = default)
        {           

            if (Cursor.Eof || !predicate(Cursor.Peek()))
            {
                return false;
            }

            var start = Cursor.Position;

            Cursor.Advance();

            while (!Cursor.Eof && predicate(Cursor.Peek()))
            {
                Cursor.Advance();
            }

            var length = Cursor.Position - start; 
            
            if (length == 0)
            {
                return false;
            }

            result?.SetToken(tokenType, Buffer, start, Cursor.Position);

            return true;
        }

        public bool ReadNonWhiteSpace(TokenResult<T> result = null, T tokenType = default)
        {
            return ReadWhile(static x => !Char.IsWhiteSpace(x), result, tokenType);
        }

        /// <summary>
        /// Reads the specified text.
        /// </summary>
        public bool ReadChar(char c, TokenResult<T> result = null, T tokenType = default)
        {
            if (!Cursor.Match(c))
            {
                return false;
            }

            if (result != null)
            {
                var start = Cursor.Position;

                Cursor.Advance();

                result?.SetToken(tokenType, Buffer, start, Cursor.Position);
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
        public bool ReadText(string text, TokenResult<T> result = null, T tokenType = default)
        {
            if (!Cursor.Match(text))
            {
                return false;
            }

            if (result != null)
            {
                var start = Cursor.Position;

                for (var i = 0; i < text.Length; i++)
                {
                    Cursor.Advance();
                }

                result?.SetToken(tokenType, Buffer, start, Cursor.Position);
            }
            else
            {
                for (var i = 0; i < text.Length; i++)
                {
                    Cursor.Advance();
                }
            }
            
            return true;
        }

        public bool ReadSingleQuotedString(TokenResult<T> result = null, T tokenType = default)
        {
            return ReadQuotedString('\'', result, tokenType);
        }

        public bool ReadDoubleQuotedString(TokenResult<T> result = null, T tokenType = default)
        {
            return ReadQuotedString('\"', result, tokenType);
        }

        public bool ReadQuotedString(TokenResult<T> result = null, T tokenType = default)
        {
            var startChar = Cursor.Peek();

            if (startChar != '\'' && startChar != '\"')
            {
                return false;
            }

            return ReadQuotedString(startChar, result, tokenType);
        }

        /// <summary>
        /// Reads a string token enclosed in single or double quotes.
        /// </summary>
        /// <remarks>
        /// This method doesn't escape the string, but only validates its content is syntactically correct.
        /// The resulting Span contains the original quotes.
        /// </remarks>
        private bool ReadQuotedString(char quoteChar, TokenResult<T> result = null, T tokenType = default)
        {
            var startChar = Cursor.Peek();

            if (startChar != quoteChar)
            {
                return false;
            }

            var start = Cursor.Position;

            Cursor.Advance();

            // Fast path if there aren't any escape char until next quote
            var startOffset = start.Offset + 1;

            var nextQuote = Cursor.Buffer.IndexOf(startChar, startOffset);

            if (nextQuote == -1)
            {
                // There is no end quote, not a string
                Cursor.ResetPosition(start);

                return false;
            }

            var nextEscape = Cursor.Buffer.IndexOf('\\', startOffset, nextQuote - startOffset);

            // If the next escape if not before the next quote, we can return the string as-is
            if (nextEscape == -1 || nextEscape > nextQuote)
            {
                for (var i = startOffset; i < nextQuote + 1; i++)
                {
                    Cursor.Advance();
                }

                result?.SetToken(tokenType, Buffer, start, Cursor.Position);
                return true;
            }

            while (!Cursor.Match(startChar))
            {
                if (Cursor.Eof)
                {
                    return false;
                }

                if (Cursor.Match('\\'))
                {
                    Cursor.Advance();

                    switch (Cursor.Peek())
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

                            if (!Cursor.Eof && Character.IsHexDigit(Cursor.Peek()))
                            {
                                Cursor.Advance();
                                if (!Cursor.Eof && Character.IsHexDigit(Cursor.Peek()))
                                {
                                    Cursor.Advance();
                                    if (!Cursor.Eof && Character.IsHexDigit(Cursor.Peek()))
                                    {
                                        Cursor.Advance();
                                        isValidUnicode = true;
                                    }
                                }
                            }

                            if (!isValidUnicode)
                            {
                                Cursor.ResetPosition(start);

                                return false;
                            }

                            break;
                        case 'x':
                            bool isValidHex = false;

                            Cursor.Advance();

                            if (!Cursor.Eof && Character.IsHexDigit(Cursor.Peek()))
                            {
                                Cursor.Advance();
                                if (!Cursor.Eof && Character.IsHexDigit(Cursor.Peek()))
                                {
                                    isValidHex = true;
                                }
                            }

                            if (!isValidHex)
                            {
                                Cursor.ResetPosition(start);

                                return false;
                            }

                            break;
                        default:
                            Cursor.ResetPosition(start);

                            return false;
                    }
                }

                Cursor.Advance();
            }

            Cursor.Advance();

            result?.SetToken(tokenType, Buffer, start, Cursor.Position);

            return true;
        }
    }
}
