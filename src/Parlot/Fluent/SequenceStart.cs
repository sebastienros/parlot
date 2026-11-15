using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

/// <summary>
/// Sets the result of a sequence.
/// </summary>
/// <remarks>
/// A parser which matches nothing, like an <see cref="Optional{T}"/> or a <see cref="ZeroOrMany{T}"/> without elements,
/// is at the cursor, before the white space the next parser skips. The sequence starts where its content does: at the
/// start of its first part, unless that part matched nothing and the next one did. A sequence of more parsers is a
/// sequence of the sequence of the first ones and the last one, so this finds the first part which matched something.
/// <para>
/// A sequence calls one of these methods where it would call <see cref="ParseResult{T}.Set"/> or set the range, and
/// nothing else: with dynamic PGO the JIT inlines the <c>Parse</c> of a sequence into the one of the longer sequence
/// it is the first part of, and doesn't when the inner one has another call or the choice written out in its body.
/// The inlined code is the same stores plus a test: the start of a first part which matched nothing is chosen in a
/// method of its own, to keep the inlined code small. Most inner sequences are then inlined as they were before the
/// start was chosen; those of the longest sequences, five parts and more, can still exceed the JIT's inlining budget.
/// </para>
/// </remarks>
internal static class SequenceStart
{
    /// <summary>
    /// Sets <paramref name="result"/> to the sequence of <paramref name="first"/> and <paramref name="next"/>, with
    /// <paramref name="value"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set<T, T1, T2>(ref ParseResult<T> result, in ParseResult<T1> first, in ParseResult<T2> next, T value)
    {
        result.Set(first.Start, next.End, value);

        if (first.Start == first.End)
        {
            result.Start = StartAfterEmpty(first.Start, next.Start, next.End);
        }
    }

    /// <summary>
    /// Sets the range of <paramref name="result"/> to the sequence of <paramref name="first"/> and <paramref name="next"/>,
    /// without a value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetRange<T, T1, T2>(ref ParseResult<T> result, in ParseResult<T1> first, in ParseResult<T2> next)
    {
        result.Start = first.Start;
        result.End = next.End;

        if (first.Start == first.End)
        {
            result.Start = StartAfterEmpty(first.Start, next.Start, next.End);
        }
    }

    // The first part matched nothing: the sequence starts where the next one does, if it matched something. Not generic,
    // so that it is compiled once and called from shared generic code without a lookup of its instantiation.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StartAfterEmpty(int firstStart, int nextStart, int nextEnd)
        => nextStart != nextEnd ? nextStart : firstStart;
}
