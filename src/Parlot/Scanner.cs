using System;

namespace Parlot
{
    public class Scanner<T>
    {
        protected readonly string Buffer;
        public Token<T> Token;
        public Cursor Cursor;        

        public Scanner(string buffer)
        {
            Buffer = buffer;
            Cursor = new Cursor(Buffer, TextPosition.Start);
        }

        public Action<Token<T>> OnToken { get; set; }

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

        public void EmitToken(T type, TextPosition offset, int length)
        {
            Token = new Token<T>(type, Buffer, offset, length);

            OnToken?.Invoke(Token);
        }

        public bool ReadIdentifier(T tokenType, Predicate<char> identifierStart, Predicate<char> identifierPart)
        {
            var start = Cursor.Position;

            if (!identifierStart(Cursor.Peek()))
            {
                return false;
            }

            Cursor.Advance();

            while (identifierPart(Cursor.Peek()))
            {
                if (Cursor.Eof)
                {
                    return false;
                }

                Cursor.Advance();
            }

            EmitToken(tokenType, start, Cursor.Position.Offset - start.Offset);

            return true;
        }

        public bool ReadDecimal(T tokenType)
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

            EmitToken(tokenType, start, length);

            return true;
        }

        /// <summary>
        /// Reads a token while the specific predicate is valid.
        /// </summary>
        public bool ReadWhile(T tokenType, Predicate<char> predicate)
        {
            var start = Cursor.Position;

            while (predicate(Cursor.Peek()))
            {
                if (Cursor.Eof)
                {
                    return false;
                }

                Cursor.Advance();
            }

            var length = Cursor.Position - start; 
            
            if (length == 0)
            {
                return false;
            }

            EmitToken(tokenType, start, length);

            return true;
        }

        public bool ReadNonWhiteSpace(T tokenType)
        {
            return ReadWhile(tokenType, x => !Char.IsWhiteSpace(x));
        }

        /// <summary>
        /// Reads the specific expected text.
        /// </summary>
        public bool ReadText(string text, T tokenType)
        {
            var start = Cursor.Position;

            if (!Cursor.Match(text))
            {
                return false;
            }

            Cursor.Advance(text.Length);

            EmitToken(tokenType, start, 1);

            return true;
        }

        public bool ReadEscapedString(T tokenType)
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
            var nextQuote = Cursor.Buffer.IndexOf(startChar, start.Offset);

            if (nextQuote != -1)
            {
                var nextEscape = Cursor.Buffer.IndexOf(startChar, start.Offset);

                // If the next escape if not before the next quote, we can return the string as-is
                if (nextEscape == -1 || nextEscape > nextQuote)
                {
                    Cursor.Advance(nextQuote - start.Offset);
                    
                    EmitToken(tokenType, start, Cursor.Position - start - 1);

                    return true;
                }
            }


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
            
            EmitToken(tokenType, start, Cursor.Position - start - 1);

            return true;
        }
    }
}
