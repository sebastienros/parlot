using System;
using System.Runtime.CompilerServices;

namespace Parlot
{
    public class Cursor
    {
        public static readonly char NullChar = '\0';

        private readonly int _textLength;
        private char _current;
        private int _offset;
        private int _line;
        private int _column;

        public Cursor(string buffer, TextPosition position)
        {
            Buffer = buffer;
            _textLength = buffer.Length;
            Eof = _textLength == 0;
            _current = _textLength == 0 ? NullChar : Buffer[position.Offset];
            _offset = 0;
            _line = 1;
            _column = 1;
        }

        public Cursor(string buffer) : this(buffer, TextPosition.Start)
        {
        }

        public TextPosition Position => new (_offset, _line, _column);

        /// <summary>
        /// Advances the cursor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count = 1)
        {
            if (Eof)
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                _offset++;

                if (_offset >= _textLength)
                {
                    Eof = true;
                    _column++;
                    _current = NullChar;
                    return;
                }

                var c = Buffer[_offset];

                if (_current == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else if (c == '\r' && PeekNext() == '\n')
                {
                    _offset++;

                    // Skip \r
                    c = '\n';
                }
                else
                {
                    _column++;
                }

                _current = c;
            }
        }

        /// <summary>
        /// Moves the cursor to the specific position
        /// </summary>
        public void ResetPosition(TextPosition position)
        {
            _offset = position.Offset;
            _line = position.Line;
            _column = position.Column;

            // Eof might have been recorded
            if (_offset >= Buffer.Length)
            {
                _current = NullChar;
                Eof = true;
            }
            else
            {
                _current = Buffer[position.Offset];
                Eof = false;
            }
        }

        /// <summary>
        /// Evaluates the char at the current position.
        /// </summary>
        public char Current => _current;

        /// <summary>
        /// Returns the cursor's position in the buffer.
        /// </summary>
        public int Offset => _offset;

        /// <summary>
        /// Evaluates a char forward in the buffer.
        /// </summary>
        public char PeekNext(int index = 1)
        {
            var nextIndex = _offset + index;

            if (nextIndex >= _textLength || nextIndex < 0)
            {
                return NullChar;
            }

            return Buffer[nextIndex];
        }
        
        public bool Eof { get; private set; }
        public string Buffer { get; private set; }

        /// <summary>
        /// Whether a char is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Match(char c)
        {
            if (Eof)
            {
                return false;
            }

            return _current == c;
        }

        /// <summary>
        /// Whether a char is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchAnyOf(string s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (Eof)
            {
                return false;
            }

            if (s.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < s.Length; i++)
            {
                if (s[i] == _current)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a char is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchAny(params char[] chars)
        {
            if (chars == null)
            {
                throw new ArgumentNullException(nameof(chars));
            }

            if (Eof)
            {
                return false;
            }

            if (chars.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] == _current)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a string is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Match(string s)
        {
            if (s.Length == 0)
            {
                return true;
            }

            if (Eof)
            {
                return false;
            }
            else if (s[0] != _current)
            {
                return false;
            }

            var length = s.Length;

            if (_offset + length - 1 >= _textLength)
            {
                return false;
            }
            
            if (length > 1 && Buffer[_offset + 1] != s[1])
            {
                return false;
            }

            for (var i = 2; i < length; i++)
            {
                if (s[i] != Buffer[_offset + i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
