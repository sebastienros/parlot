using System;

namespace Parlot
{
    using Parlot.Fluent;
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
            // Fast path if we know the current char is not a whitespace
            var current = Cursor.Current;
            if (current > ' ' && current < 256)
            {
                return false;
            }

            var offset = 0;
            var maxOffset = Cursor.Buffer.Length - Cursor.Offset;

            while (offset < maxOffset && Character.IsWhiteSpaceOrNewLine(Cursor.PeekNext(offset)))
            {
                offset++;
            }

            // We can move the cursor without tracking new lines since we know these are only spaces
            if (offset > 0)
            {
                Cursor.Advance(offset);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SkipWhiteSpace()
        {
            // Fast path if we know the current char is not a whitespace
            var current = Cursor.Current;
            if (current > ' ' && current < 256)
            {
                return false;
            }

            var offset = 0;
            var maxOffset = Cursor.Buffer.Length - Cursor.Offset;

            while (offset < maxOffset && Character.IsWhiteSpace(Cursor.PeekNext(offset)))
            {
                offset++;
            }

            // We can move the cursor without tracking new lines since we know these are only spaces
            if (offset > 0)
            {
                Cursor.AdvanceNoNewLines(offset);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadFirstThenOthers(Func<char, bool> first, Func<char, bool> other)
            => ReadFirstThenOthers(first, other, out _);

        public bool ReadFirstThenOthers(Func<char, bool> first, Func<char, bool> other, out ReadOnlySpan<char> result)
        {
            if (!first(Cursor.Current))
            {
                result = [];
                return false;
            }

            var start = Cursor.Offset;

            // At this point we have an identifier, read while it's an identifier part.

            Cursor.Advance();

            ReadWhile(other, out _);

            result = Buffer.AsSpan(start, Cursor.Offset - start);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadIdentifier() => ReadIdentifier(out _);

        public bool ReadIdentifier(out ReadOnlySpan<char> result)
        {
            // perf: using Character.IsIdentifierStart instead of x => Character.IsIdentifierStart(x) induces some allocations

            return ReadFirstThenOthers(static x => Character.IsIdentifierStart(x), static x => Character.IsIdentifierPart(x), out result);
        }

        public bool ReadBinaryNumber() => false;

        public bool ReadHexNumber() => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadDecimal() => ReadDecimal(NumberOptions.Float, out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadDecimal(out ReadOnlySpan<char> number) => ReadDecimal(NumberOptions.Float, out number);

        public bool ReadDecimal(NumberOptions options, out ReadOnlySpan<char> number, char decimalSeparator = '.', char groupSeparator = ',')
        {
            var start = Cursor.Position;
            number = [];

            if (options.HasFlag(NumberOptions.AllowLeadingSign))
            {
                if (Cursor.Current == '-' || Cursor.Current == '+')
                {
                    Cursor.AdvanceNoNewLines(1); 
                }
            }

            if (!ReadInteger(out number))
            {
                // If there is no number, check if the decimal separator is allowed and present, otherwise fail

                if (!options.HasFlag(NumberOptions.AllowDecimalSeparator) || Cursor.Current != decimalSeparator)
                {
                    Cursor.ResetPosition(start);
                    return false;
                }
            }

            // Number can be empty if we have a decimal separator directly, in this case don't expect group separators
            if (!number.IsEmpty && options.HasFlag(NumberOptions.AllowGroupSeparators) && Cursor.Current == groupSeparator)
            {
                // Group separators can be repeated as many times
                while (true)
                {
                    if (Cursor.Current == groupSeparator)
                    {
                        Cursor.AdvanceNoNewLines(1);
                    }
                    else if (!ReadInteger(out _))
                    {
                        break;
                    }
                }
            }

            if (options.HasFlag(NumberOptions.AllowDecimalSeparator))
            {
                if (Cursor.Current == decimalSeparator)
                {
                    Cursor.AdvanceNoNewLines(1);

                    ReadInteger(out number);
                }
            }

            if (options.HasFlag(NumberOptions.AllowExponent) && (Cursor.Current == 'e' || Cursor.Current == 'E'))
            {
                Cursor.AdvanceNoNewLines(1);

                if (Cursor.Current == '-' || Cursor.Current == '+')
                {
                    Cursor.AdvanceNoNewLines(1);
                }

                // The exponent must be followed by a number, without a group separator
                if (!ReadInteger(out _))
                {
                    Cursor.ResetPosition(start);
                    return false;
                }
            }

            number = Cursor.Buffer.AsSpan(start.Offset, Cursor.Offset - start.Offset);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadInteger() => ReadInteger(out _);

        public bool ReadInteger(out ReadOnlySpan<char> result)
        {
            var next = 0;
            while (Character.IsDecimalDigit(Cursor.PeekNext(next)))
            {
                next += 1;
            }

            // Not digit was read
            if (next == 0)
            {
                result = [];
                return false;
            }

            Cursor.AdvanceNoNewLines(next);                
            result = Buffer.AsSpan(Cursor.Offset - next, next);

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
        public bool ReadWhile(Func<char, bool> predicate, out ReadOnlySpan<char> result)
        {
            if (Cursor.Eof || !predicate(Cursor.Current))
            {
                result = [];
                return false;
            }

            var start = Cursor.Offset;

            Cursor.Advance();

            while (!Cursor.Eof && predicate(Cursor.Current))
            {
                Cursor.Advance();
            }

            result = Buffer.AsSpan(start, Cursor.Offset - start);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadNonWhiteSpace() => ReadNonWhiteSpace(out _);

        public bool ReadNonWhiteSpace(out ReadOnlySpan<char> result)
        {
            return ReadWhile(static x => !Character.IsWhiteSpace(x), out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadNonWhiteSpaceOrNewLine() => ReadNonWhiteSpaceOrNewLine(out _);

        public bool ReadNonWhiteSpaceOrNewLine(out ReadOnlySpan<char> result)
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
        public bool ReadChar(char c, out ReadOnlySpan<char> result)
        {
            if (!Cursor.Match(c))
            {
                result = [];
                return false;
            }

            var start = Cursor.Offset;
            Cursor.Advance();

            result = Buffer.AsSpan(start, Cursor.Offset - start);
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
        public bool ReadText(string text, StringComparison comparisonType, out ReadOnlySpan<char> result)
        {
            if (!Cursor.Match(text, comparisonType))
            {
                result = [];
                return false;
            }

            int start = Cursor.Offset;
            Cursor.Advance(text.Length);
            result = Buffer.AsSpan(start, Cursor.Offset - start);

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
        public bool ReadText(string text, out ReadOnlySpan<char> result) => ReadText(text, comparisonType: StringComparison.Ordinal, out result);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadSingleQuotedString() => ReadSingleQuotedString(out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadSingleQuotedString(out ReadOnlySpan<char> result)
        {
            return ReadQuotedString('\'', out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadDoubleQuotedString() => ReadDoubleQuotedString(out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadDoubleQuotedString(out ReadOnlySpan<char> result)
        {
            return ReadQuotedString('\"', out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadQuotedString() => ReadQuotedString(out _);

        public bool ReadQuotedString(out ReadOnlySpan<char> result)
        {
            var startChar = Cursor.Current;

            if (startChar != '\'' && startChar != '\"')
            {
                result = [];
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
        private bool ReadQuotedString(char quoteChar, out ReadOnlySpan<char> result)
        {
            var startChar = Cursor.Current;

            if (startChar != quoteChar)
            {
                result = [];
                return false;
            }

            // Fast path if there aren't any escape char until next quote
            var startOffset = Cursor.Offset + 1;

            var nextQuote = Cursor.Buffer.AsSpan(startOffset).IndexOf(startChar);

            if (nextQuote == -1)
            {
                // There is no end quote, not a string
                result = [];
                return false;
            }

            var start = Cursor.Position;

            Cursor.Advance();

            var nextEscape = Cursor.Buffer.AsSpan(startOffset, nextQuote).IndexOf('\\');

            // If the next escape if not before the next quote, we can return the string as-is
            if (nextEscape == -1)
            {
                Cursor.Advance(nextQuote + 1);

                result = Buffer.AsSpan(start.Offset, Cursor.Offset - start.Offset);
                return true;
            }

            while (nextEscape != -1)
            {
                Cursor.Advance(nextEscape);

                // We can read Eof if there is an escaped quote sequence and no actual end quote, e.g. "'abc\'def"
                if (Cursor.Eof)
                {
                    result = [];
                    return false;
                }

                if (Cursor.Match('\\'))
                {
                    Cursor.Advance();

                    switch (Cursor.Current)
                    {
                        case '0':
                        case '\\':
                        case 'a':
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

                                result = [];
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

                                result = [];
                                return false;
                            }

                            break;
                        default:
                            Cursor.ResetPosition(start);

                            result = [];
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

                    result = [];
                    return false;
                }
            }

            result = Buffer.AsSpan(start.Offset, Cursor.Offset);

            return true;
        }
    }
}
