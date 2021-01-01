using System;

namespace Parlot.Fluent
{
    public struct TextSpan : IEquatable<string>
    {
        public TextSpan(string value)
        {
            Buffer = value;
            Offset = 0;
            Length = value.Length;
            _text = value;
        }

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

        public override string ToString()
        {
            return Text;
        }

        public bool Equals(string other)
        {
            if (other == null)
            {
                return Buffer == null;
            }

            if (Length != other.Length)
            {
                return false;
            }

            return Span.SequenceEqual(other);
        }
    }
}
