using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Doesn't parse anything and return the default value.
/// </summary>
[Obsolete("Use the Then parser instead.")]
public sealed class Discard<T, U> : Parser<U>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;
    private readonly U _value;

    // Whether the parser is invoked without its value, which is replaced
    private readonly bool _withoutValue;

    public Discard(Parser<T> parser, U value)
    {
        _parser = parser;
        _value = value;
        _withoutValue = parser is not null && parser.CanParseWithoutValue;
    }

    // The hints of the parser, the only one this one invokes, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    /// <inheritdoc />
    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_withoutValue ? _parser.ParseWithoutValue(context, ref parsed) : _parser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, _value);

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
            throw new NotSupportedException("Discard requires a source-generatable parser.");
        }

        // Discard with a value cannot be source-generated
        throw new NotSupportedException(
            $"Discard<{typeof(T).Name}, {typeof(U).Name}> cannot be source-generated. " +
            $"Use .Then(static _ => yourValue) instead of .Discard(value).");
    }

    public override string ToString() => $"{_parser} (Discard)";
}
