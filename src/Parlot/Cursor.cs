using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Parlot
{
    public class Cursor
    {
        private readonly Stack<TextPosition> _stack;
        private readonly int _textLength;
        private char _current;
        private int _offset;
        private int _line;
        private int _column;

        public Cursor(string buffer, TextPosition position)
        {
            _stack = new Stack<TextPosition>();
            Buffer = buffer;
            _textLength = buffer.Length;
            _current = _textLength == 0 ? '\0' : Buffer[position.Offset];
            Eof = _textLength == 0;
            _offset = 0;
            _line = 0;
            _column = 0;
        }

        public TextPosition Position => new TextPosition(_offset, _line, _column);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Seek(TextPosition position)
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

            var span = s.AsSpan();

            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == _current)
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
            if (s.Length == 2)
            {
                return !Eof && s[0] == _current && s[1] == PeekNext();
            }

            if (Eof || _offset + s.Length - 1 >= _textLength)
            {
                return false;
            }

            var span = Buffer.AsSpan(_offset, s.Length);

            return span.SequenceEqual(s);
        }
    }
}
