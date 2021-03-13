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

#if SUPPORTS_READONLYSPAN
        public ReadOnlySpan<char> Span => Buffer == null ? ReadOnlySpan<char>.Empty : Buffer.AsSpan(Offset, Length);
#endif

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

#if NETSTANDARD2_0
            if (Length != other.Length)
            {
                return false;
            }

            for (var i = 0; i < Length; i++)
            {
                if (Buffer[Offset + i] != other[i])
                {
                    return false;
                }
            }

            return true;
#else
            return Span.SequenceEqual(other);
#endif
        }

        public bool Equals(TextSpan other)
        {
#if NETSTANDARD2_0
            if (Length != other.Length)
            {
                return false;
            }

            for (var i = 0; i < Length; i++)
            {
                if (Buffer[Offset + i] != other.Buffer[other.Offset + i])
                {
                    return false;
                }
            }

            return true;
#else
            return Span.SequenceEqual(other.Span);
#endif
        }

        public static implicit operator TextSpan(string s)
        {
            return new TextSpan(s);
        }
    }
}
