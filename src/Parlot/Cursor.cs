using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Parlot
{
    public struct Cursor
    {
        private readonly Stack<TextPosition> _stack;
        private readonly int _textLength;
        private char _current;

        public Cursor(string buffer, TextPosition position)
        {
            _stack = new Stack<TextPosition>();
            Buffer = buffer;
            Position = position;
            _textLength = buffer.Length;
            _current = _textLength == 0 ? '\0' : Buffer[position.Offset];
            Eof = _textLength == 0;
        }

        /// <summary>
        /// Records the current location of the cursor.
        /// Use this method when the current location of the text needs to be kept in case the parsing doesn't reach a successful state and
        /// another token needs to be tried.
        /// </summary>
        public void RecordPosition()
        {
            _stack.Push(Position);
        }

        /// <summary>
        /// Restores the cursor to the last recorded location.
        /// Use this method when a token wasn't found and the cursor needs to be pointing to the previously recorded location.
        /// </summary>
        public void RollbackPosition()
        {
            Seek(_stack.Pop());
        }

        /// <summary>
        /// Discard the previously recorded location.
        /// Use this method when a token was successfuly found and the recorded location can be discaded.
        /// </summary>
        public void CommitPosition()
        {
            _stack.Pop();
        }

        /// <summary>
        /// Advances the cursor.
        /// </summary>
        /// <param name="offset">The number of chars to advance the cursor of.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int offset = 1)
        {
            if (Eof)
            {
                return;
            }

            for (var i = 0; i < offset; i++)
            {
                Position = Position.NextColumn();

                Eof = Position.Offset >= _textLength;

                if (Eof)
                {
                    _current = '\0';
                    return;
                }

                var c = Buffer[Position.Offset];

                if (c == '\n' || (c == '\r' && _current != '\n'))
                {
                    Position = Position.NextLine();
                }

                _current = c;
            }
        }

        /// <summary>
        /// Moves the cursor to the specific position
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Seek(TextPosition position)
        {
            Position = position;
            _current = Buffer[position.Offset];
            Eof = false;
        }

        /// <summary>
        /// Evaluates the char at the current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char Peek()
        {
            if (Eof)
            {
                return '\0';
            }

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

            var nextIndex = Position.Offset + index;

            if (nextIndex >= _textLength || nextIndex < 0)
            {
                return '\0';
            }

            return Buffer[nextIndex];
        }
        public TextPosition Position { get; private set; }
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
        public bool MatchAny(params char[] c)
        {
            if (Eof)
            {
                return false;
            }

            for (var i = 0; i < c.Length; i++)
            {
                if (c[i] == _current)
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
            var length = s.Length;

            if (length == 1)
            {
                return !Eof && _current == s[0];
            }

            if (Eof || Position.Offset + length - 1 >= _textLength)
            {
                return false;
            }

            if (length == 2)
            {
                return s[0] == Buffer[Position.Offset] && s[1] == Buffer[Position.Offset + 1];
            }

            for (var i = 0; i < length; i++)
            {
                if (s[i] != Buffer[Position.Offset + i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
