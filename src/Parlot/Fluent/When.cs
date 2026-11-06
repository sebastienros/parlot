using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Ensure the given parser is valid based on a condition, and backtracks if not.
/// </summary>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class When<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Func<ParseContext, T, bool> _action;
    private readonly Parser<T> _parser;

    [Obsolete("Use When(Parser<T> parser, Func<ParseContext, T, bool> action) instead.")]
    public When(Parser<T> parser, Func<T, bool> action)
    {
        _action = action != null ? (c, t) => action(t) : throw new ArgumentNullException(nameof(action));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public When(Parser<T> parser, Func<ParseContext, T, bool> action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
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

        var start = context.Scanner.Cursor.Offset;

        var valid = _parser.Parse(context, ref result) && _action(context, result.Value);

        if (!valid)
        {
            context.Scanner.Cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return valid;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("When requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        
        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {cursorName}.Offset;");

        // Use helper instead of inlining
        var helperName = context.WithDiscardResult(false, () => context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_When", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName);

        // Register the action lambda
        var lambdaId = context.RegisterLambda(_action);

        // A value that doesn't meet the condition is the failure, at its start, wherever it was read up to. The start is
        // known after the white space the parser skips, which only the failure path looks for. A compacting buffer can't
        // be read again from the start, so it is skipped first.
        var skipsWhiteSpace = _parser is ISeekable { SkipWhitespace: true };
        var valueStartName = $"valueStart{context.NextNumber()}";

        if (skipsWhiteSpace && context.IsCompacting)
        {
            result.Body.Add($"if ({context.ParseContextName}.MaySkipWhiteSpace({cursorName}.Current))");
            result.Body.Add("{");
            result.Body.Add($"    {context.ParseContextName}.SkipWhiteSpace();");
            result.Body.Add("}");
            result.Body.Add($"var {valueStartName} = {cursorName}.Offset;");
        }

        var innerValueName = $"innerValue{context.NextNumber()}";
        var recordedName = $"recorded{context.NextNumber()}";
        // Written here rather than with ParseContext.RecordRejected(): a call on this path made the accepted values slower
        // on .NET 10, 14.8 ns to 28 ns on LookaheadBenchmarks.
        result.Body.Add($"if ({helperName}({context.ParseContextName}, out var {innerValueName}))");
        result.Body.Add("{");
        result.Body.Add($"    if ({lambdaId}({context.ParseContextName}, {innerValueName}))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {result.ValueVariable} = {innerValueName};");
        }
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        var {recordedName} = {cursorName}.RecordedFailure;");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add($"        {cursorName}.RestoreRecordedFailure({recordedName});");
        if (skipsWhiteSpace && context.IsCompacting)
        {
            result.Body.Add($"        {cursorName}.RecordFailure({valueStartName});");
        }
        else
        {
            if (skipsWhiteSpace)
            {
                result.Body.Add($"        {context.ParseContextName}.SkipWhiteSpace();");
            }

            result.Body.Add($"        {cursorName}.RecordFailure({cursorName}.Offset);");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        }
        result.Body.Add($"        {result.SuccessVariable} = false;");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add($"    {result.SuccessVariable} = false;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (When)";
}
