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
        /// Advances the cursor by one character.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance()
        {
            _offset++;

            if (_offset >= _textLength)
            {
                Eof = true;
                _column++;
                _current = NullChar;
                return;
            }

            var next = _buffer[_offset];

            if (_current == '\n')
            {
                _line++;
                _column = 1;
            }
            else if (next != '\r')
            {
                _column++;
            }

            // if c == '\r', don't increase the column count

            _current = next;
        }
        
        /// <summary>
        /// Advances the cursor.
        /// </summary>
        public void Advance(int count)
        {
            if (Eof)
            {
                return;
            }

            var maxOffset = _offset + count;

            // Detect if the cursor will be over Eof
            if (maxOffset > _textLength - 1)
            {
                Eof = true;
                maxOffset = _textLength - 1;
            }

            while (_offset < maxOffset)
            {
                _offset++;

                var next = _buffer[_offset];

                if (_current == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else if (next != '\r')
                {
                    _column++;
                }

                // if c == '\r', don't increase the column count

                _current = next;
            }

            if (Eof)
            {
                _current = NullChar;
                _offset = _textLength;
                _column += 1;
            }
        }

        /// <summary>
        /// Advances the cursor with the knowledge there are no new lines.
        /// </summary>
        public void AdvanceNoNewLines(int offset)
        {
            var newOffset = _offset + offset;
            var length = _textLength - 1;

            // Detect if the cursor will be over Eof
            if (newOffset > length)
            {
                Eof = true;
                _column += newOffset - length;
                _offset = _textLength;
                _current = NullChar;
                return;
            }

            _current = _buffer[newOffset];
            _offset = newOffset;
            _column += offset;
        }

        /// <summary>
        /// Moves the cursor to the specific position
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetPosition(in TextPosition position)
        {
            if (position.Offset != _offset)
            {
                ResetPositionNotInlined(position);
            }
        }

        private void ResetPositionNotInlined(in TextPosition position)
        {
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Match(string s)
        {
            // Equivalent to StringComparison.Orinal copmarison

            var sSpan = s.AsSpan();
            var bufferSpan = _buffer.AsSpan(_offset);
            
            return bufferSpan.StartsWith(sSpan);
        }

        /// <summary>
        /// Whether a string is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Match(string s, StringComparison comparisonType)
        {
            if (_textLength < _offset + s.Length)
            {
                return false;
            }

            var sSpan = s.AsSpan();
            var bufferSpan = _buffer.AsSpan(_offset);

            if (comparisonType == StringComparison.Ordinal && bufferSpan.Length > 0)
            {
                var length = sSpan.Length - 1;

                if (bufferSpan[0] != sSpan[0] || bufferSpan[length] != sSpan[length])
                {
                    return false;
                }
            }

            // StringComparison.Orinal is an optimized code path in Span.StartsWith

            return bufferSpan.StartsWith(sSpan, comparisonType);
        }
    }
}
