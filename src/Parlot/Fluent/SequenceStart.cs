using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

/// <summary>
/// The start of the result of a sequence.
/// </summary>
internal static class SequenceStart
{
    /// <summary>
    /// Returns the start of the sequence of <paramref name="first"/> and <paramref name="next"/>: the start of
    /// <paramref name="first"/>, unless it matched nothing and <paramref name="next"/> did.
    /// </summary>
    /// <remarks>
    /// A parser which matches nothing, like an <see cref="Optional{T}"/> or a <see cref="ZeroOrMany{T}"/> without elements,
    /// is at the cursor, before the white space the next parser skips. The sequence starts where its content does. A
    /// sequence of more parsers is a sequence of the sequence of the first ones and the last one, so this finds the first
    /// part which matched something.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Of<T1, T2>(in ParseResult<T1> first, in ParseResult<T2> next)
        => first.Start == first.End && next.Start != next.End ? next.Start : first.Start;
}
