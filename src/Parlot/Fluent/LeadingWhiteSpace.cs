using System;
using System.Collections.Generic;
using System.Threading;
using Parlot.Rewriting;

namespace Parlot.Fluent;

/// <summary>
/// What a parser does with the white space before what it reads first, see <see cref="LeadingWhiteSpace"/>.
/// </summary>
internal enum LeadingWhiteSpaceUse
{
    /// <summary>
    /// The parser doesn't start by skipping white space: its value starts where it is invoked.
    /// </summary>
    None,

    /// <summary>
    /// The parser skips it before it does anything else: it matches the same text, with the same value, when it was
    /// skipped before the parser is invoked. Its value starts after it.
    /// </summary>
    Skipped,

    /// <summary>
    /// The parser reads from where it is invoked, like a capture which holds the white space, a lookahead which leaves the
    /// cursor before it or a parser which can match nothing: the parser is invoked before it, and its value starts there.
    /// </summary>
    Kept,
}

/// <summary>
/// A parser which tells what it does with the white space before what it reads first, when it isn't what its
/// <see cref="ISeekable.SkipWhitespace"/> hint and the parsers it invokes first tell: see <see cref="LeadingWhiteSpace"/>.
/// </summary>
internal interface ILeadingWhiteSpace
{
    /// <summary>
    /// Gets what the parser does with it: <see cref="LeadingWhiteSpaceUse.Skipped"/> for a choice which skips it for its
    /// alternatives, <see cref="LeadingWhiteSpaceUse.Kept"/> for a parser whose match depends on it, and
    /// <see cref="LeadingWhiteSpaceUse.None"/> to let the parsers it invokes first tell.
    /// </summary>
    LeadingWhiteSpaceUse LeadingWhiteSpace { get; }
}

/// <summary>
/// A parser which skips white space and whose match depends on it, like <see cref="NoNewLineBefore{T}"/>, which looks for a
/// new line in it: though it tells that it skips white space, the white space isn't to be skipped before it is invoked.
/// </summary>
/// <remarks>
/// <para>
/// A choice whose alternatives all skip white space, and can invoke such a parser before they read anything, selects them
/// with the char after the white space and invokes them before it, and so do the parsers which skip the white space before
/// the one they invoke, see <see cref="LeadingWhiteSpace.ReadsLeadingWhiteSpace(IReadOnlyList{object})"/>. A parser type
/// implementing it calls <see cref="LeadingWhiteSpace.RegisterReader"/> in its constructor.
/// </para>
/// <para>
/// A choice can also skip the white space once and invoke its alternatives after it, when the readers they invoke first
/// only look for new lines in it: it records the white space it skipped with
/// <see cref="ParseContext.EnterSkippedWhiteSpace(int)"/>, and a reader invoked where it ends searches it, see
/// <see cref="LeadingWhiteSpace.CanSkipBeforeReaders"/>.
/// </para>
/// </remarks>
internal interface IReadsLeadingWhiteSpace
{
    /// <summary>
    /// Gets the parser it invokes after the white space.
    /// </summary>
    object Parser { get; }

    /// <summary>
    /// Gets the new lines it looks for, <see langword="null"/> for the ones of <see cref="ParseContext.WhiteSpaceSet"/>.
    /// </summary>
    string? NewLines { get; }
}

/// <summary>
/// A choice which skips the white space once for its alternatives and records it for the <see cref="IReadsLeadingWhiteSpace"/>
/// parsers they invoke first, see <see cref="ParseContext.EnterSkippedWhiteSpace(int)"/>.
/// </summary>
internal interface IRecordsSkippedWhiteSpace
{
    /// <summary>
    /// Gets whether it records the white space it skips.
    /// </summary>
    bool RecordsSkippedWhiteSpace { get; }
}

/// <summary>
/// Tells whether skipping the white space before a parser is invoked is what the parser does itself.
/// </summary>
/// <remarks>
/// <para>
/// A parser which reads where the value of another one starts, after the white space that parser skips, can't always ask
/// for it afterwards: generated helpers don't return it, and a compacting buffer can't be read again once the value is
/// read. So do the code of <c>Then((context, start, end, value) => ...)</c>, <c>ThenTry</c> and <c>When</c>, and the
/// <c>When</c> and <c>ThenTry</c> parsers reading a <c>TextReader</c>. Skipping the white space before the parser is
/// invoked is the cheapest way, and is the same as the parser skipping it for a token and for a sequence or a choice of
/// tokens. It isn't for a parser which reads from where it is invoked though the parser it reads first skips white space:
/// a <c>Capture</c> holds that white space, a lookahead leaves the cursor before it, and so does a parser which matches
/// nothing. Such a parser forwards the <see cref="ISeekable.SkipWhitespace"/> hint, which only tells that the white space
/// isn't to be looked at to find the chars it starts with. Its white space is read with
/// <see cref="ParseContext.WhiteSpaceEnd"/> instead, which doesn't move the cursor.
/// </para>
/// <para>
/// The parsers a parser invokes first are the ones <see cref="LoopAnalysis"/> explores. A parser type it doesn't know is
/// trusted on its hint.
/// </para>
/// </remarks>
internal static class LeadingWhiteSpace
{
    /// <summary>
    /// What a parser does with the white space before what it reads first, kept by the parser which asks for it while it
    /// parses until the hints of the grammar change, like <see cref="ForwardedHints"/>.
    /// </summary>
    internal sealed class Cache
    {
        public readonly int Version;
        public readonly LeadingWhiteSpaceUse Use;

        public Cache(int version, LeadingWhiteSpaceUse use)
        {
            Version = version;
            Use = use;
        }
    }

    /// <summary>
    /// Gets what <paramref name="parser"/> does with the white space before what it reads first, from
    /// <paramref name="cache"/> when the grammar didn't change since it was read.
    /// </summary>
    public static LeadingWhiteSpaceUse Of(ref Cache? cache, object parser)
    {
        // The version is read first, so that a use read while it changes is read again the next time
        var version = SeekableResolution.Version;
        var cached = Volatile.Read(ref cache);

        if (cached == null || cached.Version != version)
        {
            cached = new Cache(version, Of(parser));
            Volatile.Write(ref cache, cached);
        }

        return cached.Use;
    }

    /// <summary>
    /// Gets what <paramref name="parser"/> does with the white space before what it reads first.
    /// </summary>
    public static LeadingWhiteSpaceUse Of(object parser)
    {
        if (parser is not ISeekable { SkipWhitespace: true })
        {
            return LeadingWhiteSpaceUse.None;
        }

        if (parser is ISkipsWhiteSpace)
        {
            return LeadingWhiteSpaceUse.Skipped;
        }

        var starts = LoopAnalysis.ExploreStarts([parser]);

        return Of(parser, starts, new HashSet<object>(LoopAnalysis.ReferenceComparer.Instance));
    }

    /// <summary>
    /// Whether a parser reads the leading white space, kept by the parser which asks for it until the hints of the grammar
    /// change, like <see cref="Cache"/>.
    /// </summary>
    internal sealed class ReaderCache
    {
        public readonly int Version;
        public readonly bool Reads;

        public ReaderCache(int version, bool reads)
        {
            Version = version;
            Reads = reads;
        }
    }

    // The choices whose white space this thread resolves, see Resolving(): one reached again through a cycle keeps the
    // white space for its alternatives meanwhile
    [ThreadStatic]
    private static HashSet<object>? t_resolving;

    /// <summary>
    /// Records that <paramref name="choice"/> resolves how it skips the white space, until <see cref="EndResolving"/>.
    /// </summary>
    public static void BeginResolving(object choice) => (t_resolving ??= new HashSet<object>(LoopAnalysis.ReferenceComparer.Instance)).Add(choice);

    /// <summary>
    /// Records that <paramref name="choice"/> resolved how it skips the white space.
    /// </summary>
    public static void EndResolving(object choice) => t_resolving!.Remove(choice);

    /// <summary>
    /// Gets whether this thread resolves how <paramref name="choice"/> skips the white space, which the analysis of its
    /// alternatives reached again.
    /// </summary>
    public static bool Resolving(object choice) => t_resolving?.Contains(choice) == true;

    // Whether an IReadsLeadingWhiteSpace parser was ever built: the grammars which have none aren't explored
    private static volatile bool s_anyReader;

    /// <summary>
    /// Records that an <see cref="IReadsLeadingWhiteSpace"/> parser was built, before any grammar holding it is analyzed.
    /// </summary>
    public static void RegisterReader() => s_anyReader = true;

    /// <summary>
    /// Gets whether <paramref name="parser"/> can invoke an <see cref="IReadsLeadingWhiteSpace"/> parser before it reads
    /// anything, from <paramref name="cache"/> when the grammar didn't change since it was read.
    /// </summary>
    public static bool ReadsLeadingWhiteSpace(ref ReaderCache? cache, object parser)
    {
        var version = SeekableResolution.Version;
        var cached = Volatile.Read(ref cache);

        if (cached == null || cached.Version != version)
        {
            cached = new ReaderCache(version, ReadsLeadingWhiteSpace(parser));
            Volatile.Write(ref cache, cached);
        }

        return cached.Reads;
    }

    /// <summary>
    /// Gets whether <paramref name="parser"/> can invoke an <see cref="IReadsLeadingWhiteSpace"/> parser before it reads
    /// anything.
    /// </summary>
    public static bool ReadsLeadingWhiteSpace(object parser) => ReadsLeadingWhiteSpace([parser]);

    /// <summary>
    /// Gets whether one of <paramref name="parsers"/> can invoke an <see cref="IReadsLeadingWhiteSpace"/> parser before it
    /// reads anything.
    /// </summary>
    /// <remarks>
    /// A parser type the analysis doesn't know, like the ones of an application, isn't looked into: it is trusted not to
    /// invoke one.
    /// </remarks>
    public static bool ReadsLeadingWhiteSpace(IReadOnlyList<object> parsers)
    {
        // No grammar of the process can hold one
        if (!s_anyReader)
        {
            return false;
        }

        foreach (var start in LoopAnalysis.ExploreStarts(parsers).Keys)
        {
            if (start is IReadsLeadingWhiteSpace)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether <paramref name="parser"/> starts by skipping white space, and matches the same text, with the same
    /// value, when that white space was skipped before it is invoked.
    /// </summary>
    public static bool CanSkipBefore(object parser) => Of(parser) == LeadingWhiteSpaceUse.Skipped;

    /// <summary>
    /// The new lines the <see cref="IReadsLeadingWhiteSpace"/> parsers found by <see cref="CanSkipBeforeReaders"/> look for.
    /// </summary>
    internal sealed class ReaderNewLines
    {
        // Whether a reader was found, and whether they don't all look for the same new lines
        public bool Found;
        public bool Several;

        // The new lines of the first one
        public string? NewLines;

        public void Add(string? newLines)
        {
            if (!Found)
            {
                Found = true;
                NewLines = newLines;
            }
            else if (newLines != NewLines)
            {
                Several = true;
            }
        }
    }

    /// <summary>
    /// Gets whether each of <paramref name="parsers"/> starts by skipping white space, and matches the same text, with the
    /// same value, when that white space was skipped before it is invoked and recorded with
    /// <see cref="ParseContext.EnterSkippedWhiteSpace(int)"/>: the <see cref="IReadsLeadingWhiteSpace"/> parsers it invokes
    /// first, where it ends, look for their new lines in it. Their new lines are added to <paramref name="newLines"/>.
    /// </summary>
    /// <remarks>
    /// A reader is only invoked where the white space ends when nothing skipped white space before it: a parser which skips
    /// it first, like <see cref="SkipWhiteSpace{T}"/>, is invoked there too, and its reader would find the new lines it
    /// doesn't see. So does a reader whose parser invokes another one. Such parsers keep the white space.
    /// </remarks>
    public static bool CanSkipBeforeReaders(IReadOnlyList<object> parsers, ReaderNewLines newLines)
    {
        if (parsers.Count == 0)
        {
            return true;
        }

        // The parsers they invoke first, explored once for all of them
        var starts = LoopAnalysis.ExploreStarts(parsers);

        foreach (var parser in parsers)
        {
            if (parser is not ISeekable { SkipWhitespace: true }
                || Of(parser, starts, new HashSet<object>(LoopAnalysis.ReferenceComparer.Instance), newLines) != LeadingWhiteSpaceUse.Skipped)
            {
                return false;
            }
        }

        return true;
    }

    // Whether a reader can be invoked first by the parser, which the starts hold
    private static bool ReachesReader(object parser, Dictionary<object, LoopAnalysis.Node> starts)
    {
        if (!starts.TryGetValue(parser, out var first))
        {
            return parser is IReadsLeadingWhiteSpace;
        }

        var pending = new Stack<LoopAnalysis.Node>();
        var visited = new HashSet<LoopAnalysis.Node>();
        pending.Push(first);

        while (pending.Count > 0)
        {
            var node = pending.Pop();

            if (!visited.Add(node))
            {
                continue;
            }

            if (node.Parser is IReadsLeadingWhiteSpace)
            {
                return true;
            }

            foreach (var edge in node.Edges)
            {
                pending.Push(edge);
            }
        }

        return false;
    }

    // readers: the new lines of the IReadsLeadingWhiteSpace parsers when they are told the white space skipped before them,
    // see CanSkipBeforeReaders(), null otherwise
    private static LeadingWhiteSpaceUse Of(object parser, Dictionary<object, LoopAnalysis.Node> starts, HashSet<object> visited, ReaderNewLines? readers = null)
    {
        if (readers != null)
        {
            // A parser which can match nothing leaves the cursor where the white space ends, where a reader which follows it
            // would search the white space it skipped rather than skip none: this one included, when its parser can
            if (starts.TryGetValue(parser, out var start) && start.Nullable)
            {
                return LeadingWhiteSpaceUse.Kept;
            }

            if (parser is IReadsLeadingWhiteSpace reader)
            {
                readers.Add(reader.NewLines);
                return ReachesReader(reader.Parser, starts) ? LeadingWhiteSpaceUse.Kept : LeadingWhiteSpaceUse.Skipped;
            }

            if (parser is ISkipsWhiteSpace skips)
            {
                return ReachesReader(skips.Parser, starts) ? LeadingWhiteSpaceUse.Kept : LeadingWhiteSpaceUse.Skipped;
            }
        }

        if (parser is ISkipsWhiteSpace)
        {
            return LeadingWhiteSpaceUse.Skipped;
        }

        // A choice which records the white space it skips is invoked where that white space ends: it skips none, and keeps
        // the one recorded before, which its alternatives are told like the ones of the choice which recorded it
        var recorder = readers != null && parser is IRecordsSkippedWhiteSpace { RecordsSkippedWhiteSpace: true };

        if (!recorder && parser is ILeadingWhiteSpace { LeadingWhiteSpace: not LeadingWhiteSpaceUse.None and var declared })
        {
            return declared;
        }

        // A cycle: the other parsers decide
        if (!visited.Add(parser))
        {
            return LeadingWhiteSpaceUse.Skipped;
        }

        if (!starts.TryGetValue(parser, out var node) || node.Opaque || node.Edges.Count == 0)
        {
            // A token which skips white space itself, or a parser the analysis doesn't know
            return parser is ISeekable { SkipWhitespace: true } ? LeadingWhiteSpaceUse.Skipped : LeadingWhiteSpaceUse.Kept;
        }

        // It can leave the cursor where it was invoked, before the white space
        if (node.Nullable)
        {
            return LeadingWhiteSpaceUse.Kept;
        }

        foreach (var edge in node.Edges)
        {
            if (Of(edge.Parser, starts, visited, readers) != LeadingWhiteSpaceUse.Skipped)
            {
                return LeadingWhiteSpaceUse.Kept;
            }
        }

        return LeadingWhiteSpaceUse.Skipped;
    }
}
