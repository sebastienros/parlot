using System;

namespace Parlot
{
    public readonly struct TextSpan : IEquatable<string>, IEquatable<TextSpan>
    {
        public TextSpan(string value)
        {
            Buffer = value;
            Offset = 0;
            Length = value == null ? 0 : value.Length;
        }

        public TextSpan(string buffer, int offset, int count)
        {
            Buffer = buffer;
            Offset = offset;
            Length = count;
        }

        public readonly int Length;
        public readonly int Offset;
        public readonly string Buffer;

        public ReadOnlySpan<char> Span => Buffer == null ? ReadOnlySpan<char>.Empty : Buffer.AsSpan(Offset, Length);

        public override string ToString()
        {
            return Buffer?.Substring(Offset, Length);
        }

        public bool Equals(string other)
        {
            if (other == null)
            {
                return Buffer == null;
            }

            return Span.SequenceEqual(other.AsSpan());
        }

        public bool Equals(TextSpan other)
        {
            return Span.SequenceEqual(other.Span);
        }

        public static implicit operator TextSpan(string s)
        {
            return new TextSpan(s);
        }
    }
}
