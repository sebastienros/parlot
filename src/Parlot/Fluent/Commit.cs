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
public sealed class Commit<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;

    public Commit(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

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

        var result = sourceable.GenerateSource(context);

        // Only compacting streams have backtrack positions to release
        if (context.IsCompacting)
        {
            result.Body.Add($"if ({result.SuccessVariable}) {context.ParseContextName}.Commit();");
        }

        return result;
    }

    public override string ToString() => $"{_parser} (Commit)";
}
