using System;

namespace Parlot
{
    public readonly struct BufferSpan<T> : IEquatable<T[]>, IEquatable<BufferSpan<T>>
    where T : IEquatable<T>
    {
        public BufferSpan(T[] buffer, int offset, int count)
        {
            Buffer = buffer;
            Offset = offset;
            Length = count;
        }

#if SUPPORTS_READONLYSPAN
        public BufferSpan(Span<T> buffer, int offset, int count)
        {
            Buffer = buffer.ToArray();
            Offset = offset;
            Length = count;
        }
#endif
        public BufferSpan(T[] buffer)
        : this(buffer, 0, buffer?.Length ?? 0)
        {
        }

        public T this[int i]
        {
            get { return Buffer[Offset + i]; }
        }

        public BufferSpan<T> SubBuffer(int start, int length)
        {
            return new(Buffer, start + Offset, length);
        }

        public readonly int Length;
        public readonly int Offset;
        public readonly T[] Buffer;

#if SUPPORTS_READONLYSPAN
        public ReadOnlySpan<T> Span => Buffer == null ? ReadOnlySpan<T>.Empty : Buffer.AsSpan(Offset, Length);
#endif

        public override string ToString()
        {
            if (typeof(T) == typeof(char))
            {
                if (Buffer == null)
                    return null;
                return new string((char[])(object)Buffer, Offset, Length);
            }
            return base.ToString();
        }

        public bool Equals(T[] other)
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
                if (!Buffer[Offset + i].Equals(other[i]))
                {
                    return false;
                }
            }

            return true;
#else
            return Span.SequenceEqual(other);
#endif
        }

        public bool Equals(BufferSpan<T> other)
        {
#if NETSTANDARD2_0
            if (Length != other.Length)
            {
                return false;
            }

            for (var i = 0; i < Length; i++)
            {
                if (!Buffer[Offset + i].Equals(other.Buffer[other.Offset + i]))
                {
                    return false;
                }
            }

            return true;
#else
            return Span.SequenceEqual(other.Span);
#endif
        }

#if !NETSTANDARD2_0
        public static implicit operator BufferSpan<T>(Span<T> s)
        {
            return new BufferSpan<T>(s, 0, s.Length);
        }
        public static implicit operator ReadOnlySpan<T>(BufferSpan<T> s)
        {
            return s.Span;
        }
#endif
        public static implicit operator BufferSpan<T>(T[] s)
        {
            return new BufferSpan<T>(s, 0, s.Length);
        }

        public int IndexOf(T startChar, int startOffset = 0, int end = -1)
        {
            // #if NETSTANDARD2_0
            if (end == -1 || end > Length)
                end = Length;
            for (var i = startOffset; i < end; i++)
            {
                if (Buffer[Offset + i].Equals(startChar))
                    return i;
            }

            return -1;
            // #else
            //             return Span.IndexOf(startChar, startOffset, end);
            // #endif
        }
    }
}
