using System;

namespace Parlot
{
    public struct ParseResult<T>
    {
        public ParseResult(string buffer, TextPosition start, TextPosition end, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            Value = value;
        }

        public void Set(string buffer, TextPosition start, TextPosition end, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            Value = value;
        }

        //public void Reset()
        //{
        //    Buffer = null;
        //    Start = TextPosition.Start;
        //    End = TextPosition.Start;
        //    Length = 0;
        //    Value = default;
        //}

        public TextPosition Start { get; private set; }

        public TextPosition End { get; private set; }

        public int Length { get; private set; }
        public string Buffer { get; private set; }

        public string Text => Buffer?.Substring(Start.Offset, Length);

        public ReadOnlySpan<char> Span => Buffer.AsSpan(Start.Offset, Length);

        public T Value { get; private set; }
    }
}
