using System;

namespace Parlot
{
    public sealed class TokenResult
    {
        private string? _text;

        public bool Success { get; private set; }

        public int Start { get; private set; }

        public int End { get; private set; }

        public int Length { get; private set; }

        public string? Buffer { get; private set; }

        public string? Text => _text ??= Buffer?.Substring(Start, Length);

#if !NETSTANDARD2_0
        public ReadOnlySpan<char> Span => Buffer.AsSpan(Start, Length);
#endif
        
        public TokenResult Succeed(string buffer, int start, int end)
        {
            Success = true;
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            _text = null;

            return this;
        }

        public TokenResult Fail()
        {
            Success = false;
            Buffer = null;
            _text = null;
            Start = 0;
            End = 0;
            Length = 0;

            return this;
        }
    }
}
