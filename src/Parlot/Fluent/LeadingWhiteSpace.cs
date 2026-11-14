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
    /// Gets whether <paramref name="parser"/> starts by skipping white space, and matches the same text, with the same
    /// value, when that white space was skipped before it is invoked.
    /// </summary>
    public static bool CanSkipBefore(object parser) => Of(parser) == LeadingWhiteSpaceUse.Skipped;

    private static LeadingWhiteSpaceUse Of(object parser, Dictionary<object, LoopAnalysis.Node> starts, HashSet<object> visited)
    {
        if (parser is ISkipsWhiteSpace)
        {
            return LeadingWhiteSpaceUse.Skipped;
        }

        if (parser is ILeadingWhiteSpace { LeadingWhiteSpace: not LeadingWhiteSpaceUse.None and var declared })
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
            if (Of(edge.Parser, starts, visited) != LeadingWhiteSpaceUse.Skipped)
            {
                return LeadingWhiteSpaceUse.Kept;
            }
        }

        return LeadingWhiteSpaceUse.Skipped;
    }
}
