using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class Capture<T> : Parser<TextSpan>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<T> _parser;

    // The value of the parser isn't used, so the parsers whose value allocates don't build it
    private readonly IDiscardableParser? _discardable;

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => Hints.CanSeek;

    public char[] ExpectedChars => Hints.ExpectedChars;

    public bool SkipWhitespace => Hints.SkipWhitespace;

    private ForwardedHints Hints => ForwardedHints.Get(ref _hints, _parser, static parser => SeekableHints.Of(parser) is { CanSeek: true } hints ? hints : (false, [], false));

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    public Capture(Parser<T> parser)
    {
        _parser = parser;
        _discardable = parser as IDiscardableParser;
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Offset;

        ParseResult<T> _ = new();

        // The captured text must still be buffered once the parser is done
        var pin = context.Pin();
        var success = _discardable is not null ? _discardable.ParseWithoutValue(context) : _parser.Parse(context, ref _);
        context.Unpin(pin);

        if (success)
        {
            var end = context.Scanner.Cursor.Offset;
            var length = end - start;

            result.Set(start, end, context.Scanner.Cursor.CreateSpan(start, length));

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
            throw new NotSupportedException("Capture requires a source-generatable parser.");
        }

        if (SpanSource.TryGenerate(this, typeof(TextSpan), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(TextSpan));
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var startName = $"start{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";
        var lengthName = $"length{context.NextNumber()}";

        result.Body.Add($"var {startName} = {cursorName}.Offset;");

        // Use helper instead of inlining
        var helperName = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Capture", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName);

        // if (Helper(context, out _))
        // {
        //     var end = cursor.Offset;
        //     var length = end - start.Offset;
        //     value = new TextSpan(scanner.Buffer, start.Offset, length);
        //     success = true;
        // }
        var success = context.PinnedCall(result, $"{helperName}({context.ParseContextName}, out _)");
        result.Body.Add($"if ({success})");
        result.Body.Add("{");
        result.Body.Add($"    var {endName} = {cursorName}.Offset;");
        result.Body.Add($"    var {lengthName} = {endName} - {startName};");
        result.Body.Add($"    {result.ValueVariable} = {cursorName}.CreateSpan({startName}, {lengthName});");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        if (valueVariable == null)
        {
            writer.Generate(_parser, null, fail);
            return;
        }

        var start = writer.DeclareLocal("int", "captureStart", "0");

        writer.Add($"{start} = {writer.Pos};");

        // Only the text is captured, not the value
        writer.Generate(_parser, null, fail);
        writer.Add($"{valueVariable} = {writer.CreateSpan(start, $"{writer.Pos} - {start}")};");
    }

    public override string ToString() => $"{_parser} (Capture)";
}
