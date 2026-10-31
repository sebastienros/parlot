using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

public sealed class OneOrMany<T> : Parser<IReadOnlyList<T>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<T> _parser;
    private readonly int _max;

    public OneOrMany(Parser<T> parser) : this(parser, 0)
    {
    }

    /// <summary>Creates a parser that matches at least once and at most <paramref name="max"/> times. Zero means unlimited.</summary>
    public OneOrMany(Parser<T> parser, int max)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        ThrowHelper.ThrowIfNegative(max, nameof(max));
        _max = max;

        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        // It fails when the first element doesn't move the cursor
        return false;
    }

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();
        var previousOffset = context.Scanner.Cursor.Offset;
        if (!_parser.Parse(context, ref parsed)
            || context.Scanner.Cursor.Offset == previousOffset)
        {
            context.ExitParser(this);
            return false;
        }

        var start = parsed.Start;
        var end = parsed.End;
        var results = new ListBuilder<T>();
        results.Add(parsed.Value);

        // The start of the current element is read again when it fails
        var pin = context.Pin();

        try
        {
            while (_max == 0 || results.Count < _max)
            {
                previousOffset = context.Scanner.Cursor.Offset;
                if (!_parser.Parse(context, ref parsed)
                    || context.Scanner.Cursor.Offset == previousOffset)
                {
                    break;
                }

                context.MovePin(pin);

                end = parsed.End;
                results.Add(parsed.Value);
            }

            context.Unpin(pin);

            result.Set(start, end, results.ToArray());
        }
        finally
        {
            results.Release();
        }

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("OneOrMany requires a source-generatable parser.");
        }

        if (SpanSource.TryGenerate(this, typeof(IReadOnlyList<T>), context) is { } spanResult)
        {
            return spanResult;
        }

        var elementTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var result = context.CreateResult(typeof(IReadOnlyList<T>));

        var listName = $"list{context.NextNumber()}";

        if (!context.DiscardResult)
        {
            result.Body.Add(ListBuilderSource.Declare(listName, elementTypeName));
        }
        result.Body.Add($"{result.SuccessVariable} = false;");

        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_OneOrMany_Parser", elementTypeName, () => sourceable.GenerateSource(context))
            .MethodName;
        var previousOffsetName = $"previousOffset{context.NextNumber()}";
        var itemValueName = $"itemValue{context.NextNumber()}";
        var countName = $"count{context.NextNumber()}";

        if (_max > 0)
        {
            result.Body.Add($"int {countName} = 0;");
        }
        // Each element is read again from its start when it fails
        var pin = context.Pin(result);
        var bodyStart = result.Body.Count;
        result.Body.Add(_max == 0 ? "while (true)" : $"while ({countName} < {_max})");
        result.Body.Add("{");
        result.Body.Add($"    var {previousOffsetName} = {context.CursorName}.Offset;");
        result.Body.Add($"    if (!{helperName}({context.ParseContextName}, out var {itemValueName}) || {context.CursorName}.Offset == {previousOffsetName})");
        result.Body.Add("    {");
        result.Body.Add("        break;");
        result.Body.Add("    }");
        context.MovePin(result, pin, "    ");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {listName}.Add({itemValueName});");
        }
        result.Body.Add($"    {result.SuccessVariable} = true;");
        if (_max > 0)
        {
            result.Body.Add($"    {countName}++;");
        }
        result.Body.Add("}");
        context.Unpin(result, pin);
        if (!context.DiscardResult)
        {
            result.Body.Add($"if ({result.SuccessVariable})");
            result.Body.Add("{");
            result.Body.Add($"    {result.ValueVariable} = {listName}.ToArray();");
            result.Body.Add("}");
            ListBuilderSource.ReleaseInFinally(result, bodyStart, listName);
        }

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateLoop(writer, _parser, typeof(T), _max, oneOrMore: true, valueVariable, fail);

    public override string ToString() => $"{_parser}+";
}
