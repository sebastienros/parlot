using System;

namespace Parlot.Fluent;

/// <summary>
/// An alternative of a <c>Longest</c> choice which matched, see <see cref="LongestMatches{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the value of the alternatives.</typeparam>
public readonly struct LongestMatch<T>
{
    /// <summary>
    /// Creates a match.
    /// </summary>
    /// <param name="alternative">The index of the alternative in the list given to the choice.</param>
    /// <param name="value">The value of the alternative.</param>
    /// <param name="start">The offset where the value of the alternative starts.</param>
    /// <param name="end">The offset where the alternative stopped reading.</param>
    public LongestMatch(int alternative, T value, int start, int end) : this(alternative, value, start, end, end)
    {
    }

    internal LongestMatch(int alternative, T value, int start, int end, int resultEnd)
    {
        Alternative = alternative;
        Value = value;
        Start = start;
        End = end;
        ResultEnd = resultEnd;
    }

    /// <summary>
    /// Gets the index of the alternative in the list given to the choice.
    /// </summary>
    public int Alternative { get; }

    /// <summary>
    /// Gets the value of the alternative.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Gets the offset where the value of the alternative starts, after the white space it skipped, like the start a
    /// <c>Then</c> callback receives.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the offset where the alternative stopped reading, where the parse continues when this match is kept. The
    /// longest match is the one which ends furthest.
    /// </summary>
    public int End { get; }

    // The end of the result of the alternative, which the choice reports when it keeps this match, like the choice without
    // a selector. It is the offset where the alternative stopped reading, unless its result tells another one.
    internal int ResultEnd { get; }
}

/// <summary>
/// The alternatives of a <c>Longest</c> choice which matched at the same position, in the order of the alternatives, for
/// the selector of the choice to keep one.
/// </summary>
/// <remarks>
/// It is only valid while the selector runs: the choice reuses its storage once the selector returns, so don't keep it.
/// </remarks>
/// <typeparam name="T">The type of the value of the alternatives.</typeparam>
public readonly struct LongestMatches<T>
{
    // A copy of the builder of the choice, read through its indexer, which reads the items where the builder keeps them
    // even when the selector parses and grows the scratch stack
    private readonly ListBuilder<LongestMatch<T>> _matches;

    internal LongestMatches(in ListBuilder<LongestMatch<T>> matches)
    {
        _matches = matches;
    }

    /// <summary>
    /// Gets the number of matches, at least one.
    /// </summary>
    public int Count => _matches.Count;

    /// <summary>
    /// Gets a match.
    /// </summary>
    /// <param name="index">The index of the match, less than <see cref="Count"/>.</param>
    public LongestMatch<T> this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_matches.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _matches[index];
        }
    }

    /// <summary>
    /// Gets the index of the longest match, the first one when several end at the same offset. It is the match a choice
    /// without a selector keeps.
    /// </summary>
    public int IndexOfLongest()
    {
        if (_matches.Count == 0)
        {
            return -1;
        }

        var best = 0;

        for (var i = 1; i < _matches.Count; i++)
        {
            if (_matches[i].End > _matches[best].End)
            {
                best = i;
            }
        }

        return best;
    }
}
