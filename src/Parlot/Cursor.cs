using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Parlot
{
    public class Cursor
    {
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
            _current = _textLength == 0 ? '\0' : Buffer[position.Offset];
            _offset = 0;
            _line = 0;
            _column = 0;
        }

        public TextPosition Position => new TextPosition(_offset, _line, _column);

        /// <summary>
        /// Advances the cursor.
        /// </summary>
        /// <param name="offset">The number of c</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance()
        {
            if (Eof)
            {
                return;
            }

            _offset++;

            Eof = _offset >= _textLength;

            if (Eof)
            {
                _current = '\0';
                return;
            }

            var c = Buffer[_offset];

            if (c == '\n' || (c == '\r' && _current != '\n'))
            {
                _line++;
                _column = 0;
            }
            else
            {
                _column = 0;
            }

            _current = c;
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
                _current = '\0';
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char Peek()
        {
            return _current;
        }

        /// <summary>
        /// Evaluates a char forward in the buffer.
        /// </summary>
        public char PeekNext(int index = 1)
        {
            if (_textLength == 0)
            {
                return '\0';
            }

            var nextIndex = _offset + index;

            if (nextIndex >= _textLength || nextIndex < 0)
            {
                return '\0';
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
            if (Eof)
            {
                return false;
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
        /// Whether a string is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Match(string s)
        {
            if (s.Length == 0)
            {
                return true;
            }

            if (Eof || s[0] != _current)
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
