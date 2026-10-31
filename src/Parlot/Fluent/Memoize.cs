using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Remembers the results of a parser in the <see cref="ParseContext"/>, so that it is parsed once at a position when
/// several alternatives start with it.
/// </summary>
/// <remarks>
/// <para>
/// The results are kept for each context in a table of 8 entries indexed by the offset, the last result at an offset
/// replacing the one at an offset that shares its entry. A result is returned again, with its value, its end position and
/// the outcome of the parse, failures included, when the parser is invoked again at the same offset. A result is reused
/// as long as it is in the table, which suits the parsers that alternatives invoke one after the other at the same
/// position, not a cache for the whole input.
/// </para>
/// <para>
/// The parser's result must only depend on the text at the position. A memoized result is returned without parsing again,
/// so the callbacks of the parser, such as <c>Then</c> or <c>When</c>, are not invoked again, a value is returned to each
/// parser using it, so it shouldn't be modified, and a parser which depends on the state of a custom
/// <see cref="ParseContext"/>, like an <c>If</c>, shouldn't be memoized.
/// </para>
/// <para>
/// The white space settings of the context, the loop detection of recursive parsers and streams are taken into account:
/// a result is only reused with the same <see cref="ParseContext.WhiteSpaceParser"/> and <see cref="ParseContext.WhiteSpaceSet"/>,
/// it isn't recorded when the loop detection stopped a recursion while it was parsed or when it depended on text that
/// wasn't read yet from a stream, and it isn't reused when a recursion it parsed would be stopped.
/// </para>
/// <para>
/// Source generated parsers parse the parser itself: they don't memoize its results.
/// </para>
/// </remarks>
public sealed class Memoize<T> : Parser<T>, ISeekablePrefixes, ISourceable
{
    private readonly Parser<T> _parser;

    /// <summary>
    /// Creates a parser that memoizes the results of <paramref name="parser"/>.
    /// </summary>
    /// <param name="parser">The parser to memoize.</param>
    public Memoize(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    // The hints are read from the parser whenever they are asked for, like a Deferred, so that a choice built before a
    // deferred parser was set reads them again once it is
    /// <inheritdoc />
    public bool CanSeek => _parser is ISeekable { CanSeek: true };

    /// <inheritdoc />
    public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : [];

    /// <inheritdoc />
    public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var offset = cursor.Offset;
        var whiteSpaceParser = context.WhiteSpaceParser;
        var whiteSpaceMask = context._skipMask;
        var entries = GetEntries(context);
        ref var entry = ref entries[offset & MemoTable<T>.Mask];

        if (entry.Offset == offset
            && ReferenceEquals(entry.WhiteSpaceParser, whiteSpaceParser)
            && entry.WhiteSpaceMask == whiteSpaceMask
            && context.CanReuseMemoized(offset, entry.PushedAtStart))
        {
            if (entry.Success)
            {
                result.Set(entry.Start, entry.End, entry.Value);
                cursor.ResetPosition(entry.EndPosition);
            }

            context.ExitParser(this);
            return entry.Success;
        }

        var loopCuts = context.LoopCuts;
        var previousMinPush = context.BeginMemoized();
        bool success;
        int minPush;

        try
        {
            success = _parser.Parse(context, ref result);
        }
        finally
        {
            minPush = context.EndMemoized(previousMinPush);
        }

        // A result which depended on text after the end of a stream buffer, or on a recursion being stopped, can differ the next time
        if (!cursor.HitEnd && context.LoopCuts == loopCuts)
        {
            // The parser can have replaced the entry, at the same index, while it parsed
            entry.Offset = offset;
            entry.Success = success;
            entry.PushedAtStart = minPush <= offset;
            entry.WhiteSpaceMask = whiteSpaceMask;

            // Usually unchanged, a reference is only written when it differs
            if (!ReferenceEquals(entry.WhiteSpaceParser, whiteSpaceParser))
            {
                entry.WhiteSpaceParser = whiteSpaceParser;
            }

            if (success)
            {
                entry.Start = result.Start;
                entry.End = result.End;
                entry.EndPosition = cursor.Position;
                entry.Value = result.Value;
            }
            else
            {
                // Not kept alive by the context
                entry.Value = default!;
            }
        }

        context.ExitParser(this);
        return success;
    }

    private MemoEntry<T>[] GetEntries(ParseContext context)
    {
        var first = context.MemoTables;

        for (var table = first; table is not null; table = table.Next)
        {
            if (ReferenceEquals(table.Owner, this))
            {
                return ((MemoTable<T>)table).Entries;
            }
        }

        var created = new MemoTable<T>(this, first);
        context.MemoTables = created;
        return created.Entries;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Memoize requires a source-generatable parser.");
        }

        // Generated parsers are self-contained and don't keep results: the memoized parser is parsed each time,
        // which returns the same results
        return sourceable.GenerateSource(context);
    }

    public override string ToString() => $"{_parser} (Memoize)";
}

/// <summary>
/// The results of a memoized parser in a <see cref="ParseContext"/>, see <see cref="Memoize{T}"/>.
/// </summary>
internal abstract class MemoTable
{
    protected MemoTable(object owner, MemoTable? next)
    {
        Owner = owner;
        Next = next;
    }

    /// <summary>
    /// The memoized parser.
    /// </summary>
    public readonly object Owner;

    /// <summary>
    /// The table of another memoized parser of the same context.
    /// </summary>
    public readonly MemoTable? Next;
}

internal sealed class MemoTable<T> : MemoTable
{
    // A power of two, the entry of an offset is the one at its low bits
    private const int Size = 8;
    public const int Mask = Size - 1;

    public readonly MemoEntry<T>[] Entries = new MemoEntry<T>[Size];

    public MemoTable(object owner, MemoTable? next) : base(owner, next)
    {
        for (var i = 0; i < Entries.Length; i++)
        {
            Entries[i].Offset = -1;
        }
    }
}

internal struct MemoEntry<T>
{
    // The offset the parser was invoked at, -1 for an empty entry
    public int Offset;
    public bool Success;

    // Whether the parse marked parsers as active at the offset for the loop detection, see ParseContext.CanReuseMemoized
    public bool PushedAtStart;

    // The white space settings of the context, which parsers skipping white space depend on
    public byte WhiteSpaceMask;
    public object? WhiteSpaceParser;

    public int Start;
    public int End;
    public TextPosition EndPosition;
    public T Value;
}
