using System;
using System.Runtime.CompilerServices;

namespace Parlot
{
    public class Cursor
    {
        public const char NullChar = '\0';

        private readonly int _textLength;
        private char _current;
        private int _offset;
        private int _line;
        private int _column;
        private readonly string _buffer;

        public Cursor(string buffer, in TextPosition position)
        {
            _buffer = buffer;
            _textLength = _buffer.Length;
            Eof = _textLength == 0;
            _current = _textLength == 0 ? NullChar : _buffer[position.Offset];
            _offset = 0;
            _line = 1;
            _column = 1;
        }

        public Cursor(string buffer) : this(buffer, TextPosition.Start)
        {
        }

        public TextPosition Position => new(_offset, _line, _column);

        /// <summary>
        /// Advances the cursor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance()
        {
            if (!Eof)
            {
                AdvanceOnce();
            }
        }

        /// <summary>
        /// Advances the cursor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            if (Eof)
            {
                return;
            }

            do
            {
                if (!AdvanceOnce())
                {
                    count = 0;
                }
                count--;
            } while (count > 0);
        }

        internal bool AdvanceOnce()
        {
            _offset++;

            if (_offset >= _textLength)
            {
                Eof = true;
                _column++;
                _current = NullChar;
                return false;
            }

            var c = _buffer[_offset];

            // most probable first 
            if (_current != '\n' && c != '\r')
            {
                _column++;
            }
            else if (_current == '\n')
            {
                _line++;
                _column = 1;
            }

            // if c == '\r', don't increase the column count

            _current = c;
            return true;
        }

        /// <summary>
        /// Moves the cursor to the specific position
        /// </summary>
        public void ResetPosition(in TextPosition position)
        {
            if (position.Offset == _offset)
            {
                return;
            }

            _offset = position.Offset;
            _line = position.Line;
            _column = position.Column;

            // Eof might have been recorded
            if (_offset >= _buffer.Length)
            {
                _current = NullChar;
                Eof = true;
            }
            else
            {
                _current = _buffer[position.Offset];
                Eof = false;
            }
        }

        /// <summary>
        /// Evaluates the char at the current position.
        /// </summary>
        public char Current => _current;

        /// <summary>
        /// Returns the cursor's position in the _buffer.
        /// </summary>
        public int Offset => _offset;

        /// <summary>
        /// Evaluates a char forward in the _buffer.
        /// </summary>
        public char PeekNext(int index = 1)
        {
            var nextIndex = _offset + index;

            if (nextIndex >= _textLength || nextIndex < 0)
            {
                return NullChar;
            }

            return _buffer[nextIndex];
        }
        
        public bool Eof { get; private set; }

        public string Buffer => _buffer;

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

            // Ordinal comparison
            return _current == c;
        }

        /// <summary>
        /// Whether any char of the string is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchAnyOf(string s)
        {
            if (s == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(s));
            }

            if (Eof)
            {
                return false;
            }

            var length = s.Length;

            if (length == 0)
            {
                return true;
            }

            for (var i = 0; i < length; i++)
            {
                if (s[i] == _current)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether any char of an array is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchAny(params char[] chars)
        {
            if (chars == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(chars));
            }

            if (Eof)
            {
                return false;
            }

            var length = chars.Length;

            if (length == 0)
            {
                return true;
            }

            for (var i = 0; i < length; i++)
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
            
            if (s[0] != _current)
            {
                return false;
            }

            var length = s.Length;

            if (_offset + length - 1 >= _textLength)
            {
                return false;
            }
            
            if (length > 1 && _buffer[_offset + 1] != s[1])
            {
                return false;
            }

            for (var i = 2; i < length; i++)
            {
                if (s[i] != _buffer[_offset + i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a string is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Match(string s, StringComparer comparer)
        {
            if (s.Length == 0)
            {
                return true;
            }

            if (Eof)
            {
                return false;
            }

            var a = CharToStringTable.GetString(_current);
            var b = CharToStringTable.GetString(s[0]);

            if (comparer.Compare(a, b) != 0)
            {
                return false;
            }

            var length = s.Length;

            if (_offset + length - 1 >= _textLength)
            {
                return false;
            }

            if (length > 1)
            {
                a = CharToStringTable.GetString(_buffer[_offset + 1]);
                b = CharToStringTable.GetString(s[1]);

                if (comparer.Compare(a, b) != 0)
                {
                    return false;
                }
            }

            for (var i = 2; i < length; i++)
            {
                a = CharToStringTable.GetString(_buffer[_offset + i]);
                b = CharToStringTable.GetString(s[i]);

                if (comparer.Compare(a, b) != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
