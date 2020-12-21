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
    public class Scanner
    {
        protected readonly string Buffer;
        public Token Token;
        public Cursor Cursor;        

        public Scanner(string buffer)
        {
            Buffer = buffer;
            Cursor = new Cursor(Buffer, TextPosition.Start);
        }

        public Action<Token> OnToken { get; set; }

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

            do
            {
                Cursor.Advance();
            } while (Character.IsWhiteSpace(Cursor.Peek()));

            return true;
        }

        public void EmitToken(string type, TextPosition offset, int length)
        {
            Token = new Token(type, Buffer, offset, length);

            OnToken?.Invoke(Token);
        }

        public bool ReadIdentifier(Predicate<char> identifierStart, Predicate<char> identifierPart, string tokenType = null)
        {
            var start = Cursor.Position;

            if (!identifierStart(Cursor.Peek()))
            {
                return false;
            }

            // At this point we have an identifier, read while it's an identifier part.

            Cursor.Advance();

            ReadWhile(identifierPart);

            if (tokenType is not null)
            {
                EmitToken(tokenType, start, Cursor.Position.Offset - start.Offset);
            }

            return true;
        }

        public bool ReadDecimal(string tokenType = null)
        {
            var start = Cursor.Position;

            Cursor.RecordPosition();

            if (!Char.IsDigit(Cursor.Peek()))
            {
                Cursor.RollbackPosition();
                return false;
            }

            do
            {
                Cursor.Advance();

            } while (!Cursor.Eof && Char.IsDigit(Cursor.Peek()));

            if (Cursor.Match('.'))
            {
                Cursor.Advance();

                if (!Char.IsDigit(Cursor.Peek()))
                {
                    Cursor.RollbackPosition();
                    return false;
                }

                do
                {
                    Cursor.Advance();

                } while (!Cursor.Eof && Char.IsDigit(Cursor.Peek()));
            }

            Cursor.CommitPosition();

            var length = Cursor.Position - start; 
            
            if (length == 0)
            {
                return false;
            }

            if (tokenType is not null)
            {
                EmitToken(tokenType, start, length);
            }

            return true;
        }

        /// <summary>
        /// Reads a token while the specific predicate is valid.
        /// </summary>
        public bool ReadWhile(Predicate<char> predicate, string tokenType = null)
        {
            var start = Cursor.Position;

            while (!Cursor.Eof && predicate(Cursor.Peek()))
            {
                Cursor.Advance();
            }

            var length = Cursor.Position - start; 
            
            if (length == 0)
            {
                return false;
            }

            if (tokenType is not null)
            {
                EmitToken(tokenType, start, length);
            }

            return true;
        }

        public bool ReadNonWhiteSpace(string tokenType = null)
        {
            return ReadWhile(x => !Char.IsWhiteSpace(x), tokenType);
        }

        /// <summary>
        /// Reads the specific expected text.
        /// </summary>
        public bool ReadText(string text, string tokenType = null)
        {
            var start = Cursor.Position;

            if (!Cursor.Match(text))
            {
                return false;
            }

            Cursor.Advance(text.Length);

            if (tokenType is not null)
            {
                EmitToken(tokenType, start, 1);
            }

            return true;
        }

        /// <summary>
        /// Reads a string token enclosed in single or double quotes.
        /// </summary>
        /// <remarks>
        /// This method doesn't escape the string, but only validates its content is syntactically correct.
        /// </remarks>
        public bool ReadEscapedString(string tokenType = null)
        {
            var startChar = Cursor.Peek();

            if (startChar != '\'' && startChar != '"')
            {
                return false;
            }
            
            Cursor.RecordPosition();

            Cursor.Advance();

            var start = Cursor.Position;

            // Fast path if there aren't any escape char until next quote
            var buffer = Cursor.Buffer.AsSpan(start.Offset);

            var nextQuote = buffer.IndexOf(startChar);

            // Is there an end quote?
            if (nextQuote != -1)
            {
                var nextEscape = buffer.IndexOf('\\');

                // If the next escape if not before the next quote, we can return the string as-is
                if (nextEscape == -1 || nextEscape > nextQuote)
                {
                    Cursor.Advance(nextQuote);

                    if (tokenType is not null)
                    {
                        EmitToken(tokenType, start, Cursor.Position - start);
                    }

                    Cursor.CommitPosition();

                    return true;
                }
            }
            else
            {
                // There is no end quote
                Cursor.RollbackPosition();

                return false;
            }

            // TODO: Can we reuse the ReadOnlySpan buffer?

            while (!Cursor.Match(startChar))
            {
                if (Cursor.Eof)
                {
                    return false;
                }

                if (Cursor.Match("\\"))
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
                                Cursor.RollbackPosition();

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
                                Cursor.RollbackPosition();
                                
                                return false;
                            }

                            break;
                        default:
                            Cursor.RollbackPosition();

                            return false;
                    }
                }

                Cursor.Advance();
            }

            Cursor.Advance();

            Cursor.CommitPosition();

            if (tokenType is not null)
            {
                EmitToken(tokenType, start, Cursor.Position - start - 1);
            }

            return true;
        }
    }
}
