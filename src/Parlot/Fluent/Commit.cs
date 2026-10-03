using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Prevents any parser from moving back before the end of the parser's match once it succeeds, like a PEG cut.
/// </summary>
/// <remarks>
/// When the input is streamed through a compacting buffer, the text before the match is no longer buffered.
/// Otherwise the parser has no effect.
/// </remarks>
public sealed class Commit<T> : Parser<T>, ISeekable, ISourceable
{
    private readonly Parser<T> _parser;

    public Commit(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        if (parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (_parser.Parse(context, ref result))
        {
            context.Commit();

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Commit requires a source-generatable parser.");
        }

        // Generated parsers only parse strings, which are never compacted
        return sourceable.GenerateSource(context);
    }

    public override string ToString() => $"{_parser} (Commit)";
}
