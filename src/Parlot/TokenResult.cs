using System;

namespace Parlot
{
    public sealed class TokenResult : ITokenResult
    {
        private string _text;

        public bool Success { get; private set; }

        public TextPosition Start { get; private set; }

        public TextPosition End { get; private set; }

        public int Length { get; private set; }
        public string Buffer { get; private set; }

        public string Text => _text ??= Buffer?.Substring(Start.Offset, Length);

        public ReadOnlySpan<char> Span => Buffer.AsSpan(Start.Offset, Length);

        public ITokenResult Succeed(string buffer, in TextPosition start, in TextPosition end)
        {
            Success = true;
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            _text = null;

            return this;
        }

        public ITokenResult Fail()
        {
            Success = false;
            Buffer = null;
            _text = null;
            Start = TextPosition.Start;
            End = TextPosition.Start;
            Length = 0;

            return this;
        }
    }
}
