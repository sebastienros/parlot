using System;

namespace Parlot
{
    public sealed class TokenResult : ITokenResult
    {
        private string _buffer;
        private string _text;

        public bool Success { get; private set; }

        public TextPosition Start { get; private set; }

        public TextPosition End { get; private set; }

        public int Length { get; private set; }

        public string Text => _text ??= _buffer?.Substring(Start.Offset, Length);

        public ReadOnlySpan<char> Span => _buffer.AsSpan(Start.Offset, Length);

        public ITokenResult Set(string buffer, TextPosition start, TextPosition end)
        {
            Success = true;
            _buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;

            return this;
        }

        public ITokenResult Reset()
        {
            Success = false;
            _buffer = null;
            _text = null;
            Start = End = TextPosition.Start;

            return this;
        }
    }
}
