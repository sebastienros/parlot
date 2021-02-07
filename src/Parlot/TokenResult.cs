using System;

namespace Parlot
{
    using System.Runtime.CompilerServices;

    public readonly struct TokenResult
    {
        private readonly string _buffer;
        
        public readonly int Start;
        public readonly int Length;

        private TokenResult(string buffer, int start, int length)
        {
            _buffer = buffer;
            Start = start;
            Length = length;
        }

        public string GetText() => _buffer.Substring(Start, Length);

#if !NETSTANDARD2_0
        public ReadOnlySpan<char> Span => _buffer.AsSpan(Start, Length);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TokenResult Succeed(string buffer, int start, int end)
        {
            return new(buffer, start, end - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TokenResult Fail() => default;
    }
}
