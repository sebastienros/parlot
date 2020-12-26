using System;

namespace Parlot.Fluent
{
    public struct TextSpan
    {
        public TextSpan(string buffer, int offset, int count)
        {
            Buffer = buffer;
            Offset = offset;
            Length = count;
            _text = null;
        }

        private string _text;
        public int Length { get; private set; }
        public int Offset { get; private set; }
        public string Buffer { get; private set; }

        public string Text => _text ??= Buffer?.Substring(Offset, Length);

        public ReadOnlySpan<char> Span => Buffer.AsSpan(Offset, Length);
    }
}
