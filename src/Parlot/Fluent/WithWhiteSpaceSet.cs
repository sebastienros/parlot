using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// A parser that sets the <see cref="ParseContext.WhiteSpaceSet"/> of its inner parser.
/// </summary>
public sealed class WithWhiteSpaceSet<T> : Parser<T>, ISeekable, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;
    private readonly WhiteSpaceSet _whiteSpaceSet;

    // Whether the parser is invoked without its value when this one's isn't used
    private readonly bool _withoutValue;

    public WithWhiteSpaceSet(Parser<T> parser, WhiteSpaceSet whiteSpaceSet)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        // Validates the set
        Character.GetWhiteSpaceMask(whiteSpaceSet);
        _whiteSpaceSet = whiteSpaceSet;
        _withoutValue = parser.CanParseWithoutValue;
    }

    public WhiteSpaceSet WhiteSpaceSet => _whiteSpaceSet;

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later. A parser
    // skipping white spaces does it with this set, which a OneOf doesn't know about: it would skip them with its own
    // before looking up the first char.
    private ForwardedHints? _hints;

    public bool CanSeek => Hints.CanSeek;

    public char[] ExpectedChars => Hints.ExpectedChars;

    public bool SkipWhitespace { get; }

    private ForwardedHints Hints => ForwardedHints.Get(ref _hints, _parser, static parser => SeekableHints.Of(parser) is { SkipWhitespace: false } hints ? hints : (false, [], false));

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var previousWhiteSpaceSet = context.WhiteSpaceSet;

        try
        {
            context.WhiteSpaceSet = _whiteSpaceSet;

            var success = _parser.Parse(context, ref result);

            context.ExitParser(this);
            return success;
        }
        finally
        {
            context.WhiteSpaceSet = previousWhiteSpaceSet;
        }
    }

    internal override bool CanParseWithoutValue => _withoutValue;

    // Parse, with the parser invoked without its value
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var previousWhiteSpaceSet = context.WhiteSpaceSet;

        try
        {
            context.WhiteSpaceSet = _whiteSpaceSet;

            var success = _withoutValue ? _parser.ParseWithoutValue(context, ref result) : _parser.Parse(context, ref result);

            context.ExitParser(this);
            return success;
        }
        finally
        {
            context.WhiteSpaceSet = previousWhiteSpaceSet;
        }
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("WithWhiteSpaceSet requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var previousName = $"previousWhiteSpaceSet{context.NextNumber()}";

        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_WithWhiteSpaceSet", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        result.Body.Add($"var {previousName} = {ctx}.WhiteSpaceSet;");
        result.Body.Add($"{ctx}.WhiteSpaceSet = global::Parlot.WhiteSpaceSet.{_whiteSpaceSet};");
        result.Body.Add("try");
        result.Body.Add("{");
        if (context.DiscardResult)
        {
            result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out _);");
        }
        else
        {
            result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out {result.ValueVariable});");
        }
        result.Body.Add("}");
        result.Body.Add("finally");
        result.Body.Add("{");
        result.Body.Add($"    {ctx}.WhiteSpaceSet = {previousName};");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (WS {_whiteSpaceSet})";
}
