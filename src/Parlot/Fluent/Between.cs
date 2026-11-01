using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// This parser parses a value between two other parsers. It returns the value parsed by the middle parser
/// making it easier to skip delimiters than writing <code>a.SkipAnd(b).AndSkip(c)</code>.
/// </summary>
/// <typeparam name="A">The type of the parser before the main parser.</typeparam>
/// <typeparam name="T">The type of the value parsed by the main parser.</typeparam>
/// <typeparam name="B">The type of the parser after the main parser.</typeparam>
public sealed class Between<A, T, B> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<T> _parser;
    private readonly Parser<A> _before;
    private readonly Parser<B> _after;

    public Between(Parser<A> before, Parser<T> parser, Parser<B> after)
    {
        _before = before ?? throw new ArgumentNullException(nameof(before));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _after = after ?? throw new ArgumentNullException(nameof(after));
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _before).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _before).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _before).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_before);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_before) && analysis.Invoke(_parser) && analysis.Invoke(_after);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        var start = cursor.Offset;

        var parsedA = new ParseResult<A>();

        if (!_before.Parse(context, ref parsedA))
        {
            context.ExitParser(this);

            // Don't reset position since _before should do it
            return false;
        }

        if (!_parser.Parse(context, ref result))
        {
            cursor.ResetPosition(start);

            context.ExitParser(this);
            return false;
        }

        var parsedB = new ParseResult<B>();

        if (!_after.Parse(context, ref parsedB))
        {
            cursor.ResetPosition(start);

            context.ExitParser(this);
            return false;
        }

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_before is not ISourceable beforeSourceable || _parser is not ISourceable parserSourceable || _after is not ISourceable afterSourceable)
        {
            throw new NotSupportedException("Between requires all parsers to be source-generatable.");
        }

        if (SpanSource.TryGenerate(this, typeof(T), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = false;");

        string Helper(ISourceable p, Type valueType, string suffix)
        {
            var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
            return context.Helpers
                .GetOrCreate(p, $"{context.MethodNamePrefix}_Between_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperBefore = context.WithDiscardResult(true, () => Helper(beforeSourceable, typeof(A), "Before"));
        var helperParser = Helper(parserSourceable, typeof(T), "Parser");
        var helperAfter = context.WithDiscardResult(true, () => Helper(afterSourceable, typeof(B), "After"));

        result.Body.Add($"if ({helperBefore}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        if (context.DiscardResult)
        {
            result.Body.Add($"    if ({helperParser}({context.ParseContextName}, out _))");
        }
        else
        {
            result.Body.Add($"    if ({helperParser}({context.ParseContextName}, out {result.ValueVariable}))");
        }
        result.Body.Add("    {");
        result.Body.Add($"        if ({helperAfter}({context.ParseContextName}, out _))");
        result.Body.Add("        {");
        result.Body.Add($"            {result.SuccessVariable} = true;");
        result.Body.Add("        }");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add($"if (!{result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _before, _parser, _after);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail,
            [(_before, typeof(A), false), (_parser, typeof(T), true), (_after, typeof(B), false)],
            static values => values[1]!);

    public override string ToString() => Name ?? $"Between({_before},{_parser},{_after})";
}
