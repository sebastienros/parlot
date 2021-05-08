using System;
using System.Runtime.CompilerServices;

namespace Parlot
{
    public class Cursor<T>
    where T : IEquatable<T>, IConvertible
    {
        public static readonly T NullChar = default(T);

        private readonly int _textLength;
        private T _current;
        private int _offset;
        private int _line;
        private int _column;
        private readonly BufferSpan<T> _buffer;

        public Cursor(BufferSpan<T> buffer, in TextPosition position)
        {
            _buffer = buffer;
            _textLength = buffer.Length;
            Eof = _textLength == 0;
            _current = _textLength == 0 ? NullChar : buffer[position.Offset];
            _offset = 0;
            _line = 1;
            _column = 1;
            this.IsChar = typeof(T) == typeof(char);
        }

        public readonly bool IsChar;

        public Cursor(BufferSpan<T> buffer) : this(buffer, TextPosition.Start)
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
            this._offset = _offset + 1;

            if (_offset >= _textLength)
            {
                Eof = true;
                _column++;
                _current = NullChar;
                return false;
            }

            var c = PeekNext(0);

            if (IsChar)
            {
                var currentAsChar = _current.ToChar(null);
                // most probable first 
                if (currentAsChar != '\n' && c.ToChar(null) != '\r')
                {
                    _column++;
                }
                else if (currentAsChar == '\n')
                {
                    _line++;
                    _column = 1;
                }
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
            if (_offset >= _textLength)
            {
                _current = NullChar;
                Eof = true;
            }
            else
            {
                _current = _buffer[_offset];
                Eof = false;
            }
        }

        /// <summary>
        /// Evaluates the char at the current position.
        /// </summary>
        public T Current => _current;

        /// <summary>
        /// Returns the cursor's position in the _buffer.
        /// </summary>
        public int Offset => _offset;

        /// <summary>
        /// Evaluates a char forward in the _buffer.
        /// </summary>
        public T PeekNext(int index = 1)
        {
            var nextIndex = _offset + index;

            if (nextIndex >= _textLength || nextIndex < 0)
            {
                return NullChar;
            }

            return _buffer[nextIndex];
        }

        public bool Eof { get; private set; }

        public BufferSpan<T> Buffer => _buffer;

        /// <summary>
        /// Whether a char is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Match(T c)
        {
            if (Eof)
            {
                return false;
            }

            // Ordinal comparison
            return _current.Equals(c);
        }

        /// <summary>
        /// Whether any char of the string is at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchAnyOf(T[] s)
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
                if (s[i].Equals(_current))
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
        public bool MatchAny(params T[] chars)
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
                if (chars[i].Equals(_current))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a string is at the current position.
        /// </summary>
        public bool Match(T[] s)
        {
            if (s.Length == 0)
            {
                return true;
            }

            if (Eof)
            {
                return false;
            }

            if (!s[0].Equals(_current))
            {
                return false;
            }

            var length = s.Length;

            if (_offset + length - 1 >= _textLength)
            {
                return false;
            }

            if (length > 1 && !PeekNext(1).Equals(s[1]))
            {
                return false;
            }

            for (var i = 2; i < length; i++)
            {
                if (!s[i].Equals(PeekNext(i)))
                {
                    return false;
                }
            }

            return true;
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

            if (Eof || !IsChar)
            {
                return false;
            }

            if (!s[0].Equals(_current))
            {
                return false;
            }

            var length = s.Length;

            if (_offset + length - 1 >= _textLength)
            {
                return false;
            }

            if (length > 1 && !PeekNext(1).Equals(s[1]))
            {
                return false;
            }

            for (var i = 2; i < length; i++)
            {
                if (!s[i].Equals(PeekNext(i)))
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

            if (Eof || !IsChar)
            {
                return false;
            }

            var a = CharToStringTable.GetString(_current.ToChar(null));
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
                a = CharToStringTable.GetString(PeekNext(1).ToChar(null));
                b = CharToStringTable.GetString(s[1]);

                if (comparer.Compare(a, b) != 0)
                {
                    return false;
                }
            }

            for (var i = 2; i < length; i++)
            {
                a = CharToStringTable.GetString(PeekNext(i).ToChar(null));
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
