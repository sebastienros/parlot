using System.Threading;

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

    // See Version
    private static int _version;

    /// <summary>
    /// Gets a number that changes when the hints of a parser can have changed: a deferred parser was set, or a choice built
    /// its lookup table again. The parsers that read their hints from other parsers keep them until it changes.
    /// </summary>
    public static int Version => Volatile.Read(ref _version);

    /// <summary>
    /// Tells the parsers that read their hints from other parsers to read them again.
    /// </summary>
    public static void Invalidate() => Interlocked.Increment(ref _version);

    /// <summary>
    /// Gets whether the current thread is resolving hints, such that the choices it reaches can build their lookup tables.
    /// </summary>
    public static bool IsResolving => Monitor.IsEntered(SyncRoot);
}

/// <summary>
/// A parser whose <see cref="ISeekable"/> hints are only complete once the grammar is, like a choice holding a deferred parser
/// that wasn't set when the choice was built.
/// </summary>
internal interface IResolvableSeekable : ISeekable
{
    /// <summary>
    /// Builds the hints again from the ones the parsers it invokes declare now, unless they were already complete.
    /// </summary>
    void EnsureResolved();
}
