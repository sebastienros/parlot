using System;

namespace Parlot.Fluent
{
    public readonly struct TextSpan : IEquatable<string>, IEquatable<TextSpan>
    {
        public TextSpan(string value)
        {
            Buffer = value;
            Offset = 0;
            Length = value.Length;
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

        public ReadOnlySpan<char> Span => Buffer.AsSpan(Offset, Length);

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

            if (Length != other.Length)
            {
                return false;
            }

            return Span.SequenceEqual(other);
        }

        public bool Equals(TextSpan other)
        {
            return Span.SequenceEqual(other.Span);
        }
    }
}
