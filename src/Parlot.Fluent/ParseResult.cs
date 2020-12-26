using System;

namespace Parlot
{
    public struct ParseResult<T>
    {
        public static ParseResult<T> Empty = new(null, TextPosition.Start, TextPosition.Start, default);

        public ParseResult(string buffer, TextPosition start, TextPosition end, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            _text = null;
            _typedValue = value;
        }

        public TextPosition Start { get; private set; }

        public TextPosition End { get; private set; }

        public int Length { get; private set; }
        public string Buffer { get; private set; }

        private string _text;
        public string Text => _text ??= Buffer?.Substring(Start.Offset, Length);

        public ReadOnlySpan<char> Span => Buffer.AsSpan(Start.Offset, Length);

        private readonly T _typedValue;

        public T GetValue() => _typedValue;
    }
}
