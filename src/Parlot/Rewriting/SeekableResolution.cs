namespace Parlot.Rewriting;

/// <summary>
/// Serializes the resolution of the <see cref="ISeekable"/> hints that are only known once a grammar is complete.
/// </summary>
internal static class SeekableResolution
{
    /// <summary>
    /// A single lock for every parser, so that two threads resolving a cycle of parsers from different ends never wait for each other.
    /// </summary>
    public static readonly object SyncRoot = new();
}
