using System;

namespace Parlot
{
    using System.Runtime.CompilerServices;

    public static class TokenResult
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferSpan<T> Succeed<T>(BufferSpan<T> buffer, int start, int end)
        where T : IEquatable<T>
        {
            return buffer.SubBuffer(start, end - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferSpan<T> Fail<T>()
        where T : IEquatable<T>
             => default;
    }
}
