using System;

namespace Parlot
{
    public class ParseResult<T> : IParseResult<T>
    {
        private string _text;

        public bool Success { get; private set; }

        public TextPosition Start { get; private set; }

        public TextPosition End { get; private set; }

        public int Length { get; private set; }
        public string Buffer { get; private set; }
        private T _value;
        public string Text => _text ??= Buffer?.Substring(Start.Offset, Length);

        public ReadOnlySpan<char> Span => Buffer.AsSpan(Start.Offset, Length);

        public void Fail()
        {
            Success = false;
            Buffer = null;
            _text = null;
            Start = TextPosition.Start;
            End = TextPosition.Start;
            Length = 0;
            _value = default;
        }

        public void Succeed(string buffer, TextPosition start, TextPosition end, T value)
        {
            Success = true;
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            _text = null;
            _value = value;
        }

        public T Value => _value;
    }
}
