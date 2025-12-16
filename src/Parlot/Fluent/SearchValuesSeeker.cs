#if NET8_0_OR_GREATER
using System;
using System.Buffers;

namespace Parlot.Fluent;

/// <summary>
/// Utility class for tracking the next match position in a text span based on SearchValues.
/// Optimizes parser performance by caching the position of the next potential match.
/// </summary>
internal sealed class SearchValuesSeeker
{
    private const int NotSearched = -1;
    private const int NoMatchFound = -2;

    private readonly SearchValues<char> _searchValues;
    private int _nextMatch = NotSearched;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchValuesSeeker"/> class.
    /// </summary>
    /// <param name="searchValues">The SearchValues instance used to find matches.</param>
    public SearchValuesSeeker(SearchValues<char> searchValues)
    {
        _searchValues = searchValues;
    }

    /// <summary>
    /// Checks if the current offset matches the expected next match position.
    /// Returns true if in default state (not yet searched) or if the offset matches the expected position.
    /// Returns false if a previous search found no match or if past the expected position.
    /// </summary>
    /// <param name="currentOffset">The current offset to check.</param>
    /// <returns>True if at the expected position or not yet searched; false otherwise.</returns>
    public bool CanMatch(int currentOffset)
    {
        if (_nextMatch == NotSearched)
        {
            return true;
        }
        else if (_nextMatch == NoMatchFound)
        {
            return false;
        }
        else
        {
            // Check if we're at the expected position or after it, if so return true
            return currentOffset >= _nextMatch;
        }
    }

    /// <summary>
    /// Searches for and updates the next match position starting from the current offset.
    /// </summary>
    /// <param name="currentOffset">The current offset to start searching from.</param>
    /// <param name="span">The span to search for the next match.</param>
    public void UpdateNextMatch(int currentOffset, ReadOnlySpan<char> span)
    {
        var index = span.Slice(currentOffset).IndexOfAny(_searchValues);
        _nextMatch = index >= 0 ? currentOffset + index : NoMatchFound;
    }

    /// <summary>
    /// Resets the next match position to uninitialized state.
    /// </summary>
    public void Reset()
    {
        _nextMatch = NotSearched;
    }

    /// <summary>
    /// Gets the current next match offset, or -1 if not set.
    /// </summary>
    public int NextMatch => _nextMatch;
}
#endif
