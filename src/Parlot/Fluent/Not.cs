using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class Not<T> : Parser<T>, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;

    // Whether the parser is invoked without its value, which isn't used
    private readonly bool _withoutValue;

    public Not(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _withoutValue = parser.CanParseWithoutValue;
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        return true;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Offset;

        // The text is read again when the parser fails
        var pin = context.Pin();
        var success = _withoutValue ? _parser.ParseWithoutValue(context, ref result) : _parser.Parse(context, ref result);
        context.Unpin(pin);

        if (!success)
        {
            context.ExitParser(this);
            return true;
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Not requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {cursorName}.Offset;");

        // Use helper instead of inlining
        var helperName = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Not", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName);

        // if (Helper(context, out _))
        // {
        //     // The failure is at the start of what matched, wherever it was read up to
        //     context.RecordRejected(start, recorded, skipsWhiteSpace);
        //     cursor.ResetPosition(start);
        //     success = false;
        // }
        // else
        // {
        //     // What it read, to find that it doesn't match, says nothing about what follows
        //     cursor.RestoreRecordedFailure(recorded);
        //     success = true;
        // }
        var recordedName = $"recorded{context.NextNumber()}";
        result.Body.Add($"var {recordedName} = {cursorName}.RecordedFailure;");
        var success = context.PinnedCall(result, $"{helperName}({context.ParseContextName}, out _)");
        result.Body.Add($"if ({success})");
        result.Body.Add("{");
        result.Body.Add($"    {context.ParseContextName}.RecordRejected({startName}, {recordedName}, {(_parser is ISeekable { SkipWhitespace: true } ? "true" : "false")});");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add($"    {result.SuccessVariable} = false;");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.RestoreRecordedFailure({recordedName});");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"Not ({_parser})";
}
