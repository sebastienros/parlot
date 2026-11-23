using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

public sealed class OneOrMany<T> : Parser<IReadOnlyList<T>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<T> _parser;
    private readonly int _max;

    // Whether the elements are parsed without their value when the list isn't built
    private readonly bool _withoutValue;

    // Whether the parser can start at the current position, so the loop stops without invoking it when it can't
    private readonly SeekGuard _guard;

    public OneOrMany(Parser<T> parser) : this(parser, 0)
    {
    }

    /// <summary>Creates a parser that matches at least once and at most <paramref name="max"/> times. Zero means unlimited.</summary>
    public OneOrMany(Parser<T> parser, int max)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        ThrowHelper.ThrowIfNegative(max, nameof(max));
        _max = max;
        _withoutValue = parser.CanParseWithoutValue;
        _guard = new SeekGuard(parser);
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

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
                if (!_guard.CanStart(context)
                    || !_parser.Parse(context, ref parsed)
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

    internal override bool CanParseWithoutValue => true;

    // The loop of Parse, without the list
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();
        var previousOffset = context.Scanner.Cursor.Offset;
        if (!(_withoutValue ? _parser.ParseWithoutValue(context, ref parsed) : _parser.Parse(context, ref parsed))
            || context.Scanner.Cursor.Offset == previousOffset)
        {
            context.ExitParser(this);
            return false;
        }

        var start = parsed.Start;
        var end = parsed.End;
        var count = 1;

        // The start of the current element is read again when it fails
        var pin = context.Pin();

        while (_max == 0 || count < _max)
        {
            previousOffset = context.Scanner.Cursor.Offset;
            if (!_guard.CanStart(context)
                || !(_withoutValue ? _parser.ParseWithoutValue(context, ref parsed) : _parser.Parse(context, ref parsed))
                || context.Scanner.Cursor.Offset == previousOffset)
            {
                break;
            }

            context.MovePin(pin);

            end = parsed.End;
            count++;
        }

        context.Unpin(pin);

        result.Start = start;
        result.End = end;

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

        result.Body.Add($"{result.SuccessVariable} = false;");

        var rawHelperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_OneOrMany_Parser", elementTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // Subsequent elements aren't parsed when they can't start at the current position (matching runtime Parse)
        var guardedHelperName = _max == 1 ? rawHelperName : SeekGuard.GetHelper(context, _parser, rawHelperName, typeof(T));

        if (!context.DiscardResult && !context.IsCompacting)
        {
            ListBuilderSource.EmitFirstItemThenRest(context, result, rawHelperName, guardedHelperName, elementTypeName, _max);
            return result;
        }

        var ctx = context.ParseContextName;
        var cursor = context.CursorName;
        var listName = $"list{context.NextNumber()}";
        var previousOffsetName = $"previousOffset{context.NextNumber()}";
        var firstItemName = $"firstItem{context.NextNumber()}";
        var itemValueName = $"itemValue{context.NextNumber()}";
        var countName = $"count{context.NextNumber()}";
        var firstOutTarget = context.DiscardResult ? "_" : $"var {firstItemName}";
        var nextOutTarget = context.DiscardResult ? "_" : $"var {itemValueName}";

        result.Body.Add($"var {previousOffsetName} = {cursor}.Offset;");
        result.Body.Add($"if ({rawHelperName}({ctx}, out {firstOutTarget}) && {cursor}.Offset != {previousOffsetName})");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");

        if (_max == 1)
        {
            if (!context.DiscardResult)
            {
                result.Body.Add($"    {result.ValueVariable} = new {elementTypeName}[] {{ {firstItemName} }};");
            }
        }
        else
        {
            var bodyStart = -1;
            if (!context.DiscardResult)
            {
                result.Body.Add($"    {ListBuilderSource.Declare(listName, elementTypeName)}");
                result.Body.Add($"    {listName}.Add({firstItemName});");
                bodyStart = result.Body.Count;
            }
            if (_max > 0)
            {
                result.Body.Add($"    int {countName} = 1;");
            }
            // Each subsequent element is read again from its start when it fails
            string? pin = null;
            if (context.IsCompacting)
            {
                pin = $"pin{context.NextNumber()}";
                result.Body.Add($"    var {pin} = {ctx}.Pin();");
            }
            result.Body.Add(_max == 0 ? "    while (true)" : $"    while ({countName} < {_max})");
            result.Body.Add("    {");
            result.Body.Add($"        {previousOffsetName} = {cursor}.Offset;");
            result.Body.Add($"        if (!{guardedHelperName}({ctx}, out {nextOutTarget}) || {cursor}.Offset == {previousOffsetName})");
            result.Body.Add("        {");
            result.Body.Add("            break;");
            result.Body.Add("        }");
            context.MovePin(result, pin, "        ");
            if (!context.DiscardResult)
            {
                result.Body.Add($"        {listName}.Add({itemValueName});");
            }
            if (_max > 0)
            {
                result.Body.Add($"        {countName}++;");
            }
            result.Body.Add("    }");
            context.Unpin(result, pin, "    ");
            if (!context.DiscardResult)
            {
                result.Body.Add($"    {result.ValueVariable} = {listName}.ToArray();");
                ListBuilderSource.ReleaseInFinally(result, bodyStart, listName, "    ");
            }
        }

        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateLoop(writer, _parser, typeof(T), _max, oneOrMore: true, valueVariable, fail);

    public override string ToString() => FormatOnce($"{_parser}+");
}
