using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Runtime.CompilerServices;

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

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        // A value that doesn't meet the condition is the failure, at its start, wherever it was read up to, as in generated
        // code. The start is known after the white space the parser skips, which only the failure path looks for. A
        // compacting buffer can't be read again from the start, so where the white space ends is read first.
        var valueStart = context.IsCompacting ? ReadValueStart(context) : -1;

        if (!_parser.Parse(context, ref result))
        {
            cursor.ResetPosition(start);
            context.ExitParser(this);
            return false;
        }

        if (!_action(context, result.Value))
        {
            var recorded = cursor.RecordedFailure;

            if (valueStart >= 0)
            {
                cursor.ResetPosition(start);
                cursor.RestoreRecordedFailure(recorded);
                cursor.RecordFailure(valueStart);
            }
            else
            {
                context.RecordRejected(start, recorded, _parser is ISeekable { SkipWhitespace: true });
                cursor.ResetPosition(start);
            }

            context.ExitParser(this);
            return false;
        }

        context.ExitParser(this);
        return true;
    }

    // What the parser does with the white space before it, read when it is first asked for since the parser can be a
    // Deferred set later
    private LeadingWhiteSpace.Cache? _leading;

    // Where the value starts when the buffer is a compacting one, after the white space the parser skips, or -1 when it
    // doesn't skip any. Not inlined: parsing a string doesn't pay for it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ReadValueStart(ParseContext context)
    {
        var leading = LeadingWhiteSpace.Of(ref _leading, _parser);

        if (leading == LeadingWhiteSpaceUse.None)
        {
            return -1;
        }

        var cursor = context.Scanner.Cursor;

        if (!context.MaySkipWhiteSpace(cursor.Current))
        {
            return cursor.Offset;
        }

        if (leading == LeadingWhiteSpaceUse.Skipped)
        {
            // The parser skips it before anything else, like a token, a sequence or a choice of tokens: it is skipped
            // for it, and isn't kept
            context.SkipWhiteSpace();

            return cursor.Offset;
        }

        // Any other parser is invoked where a string parser invokes it: a capture holds the white space it skips, and a
        // lookahead leaves the cursor before it
        return context.WhiteSpaceEnd();
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
        // be read again from the start, so where the white space ends is read first: it is skipped for a parser which
        // would skip it first itself, and read without moving the cursor for one which reads from where it is invoked,
        // like a capture or a lookahead.
        var skipsWhiteSpace = _parser is ISeekable { SkipWhitespace: true };
        var valueStartName = $"valueStart{context.NextNumber()}";

        if (skipsWhiteSpace && context.IsCompacting)
        {
            if (LeadingWhiteSpace.CanSkipBefore(_parser))
            {
                result.Body.Add($"if ({context.ParseContextName}.MaySkipWhiteSpace({cursorName}.Current))");
                result.Body.Add("{");
                result.Body.Add($"    {context.ParseContextName}.SkipWhiteSpace();");
                result.Body.Add("}");
                result.Body.Add($"var {valueStartName} = {cursorName}.Offset;");
            }
            else
            {
                result.Body.Add($"var {valueStartName} = {startName};");
                result.Body.Add($"if ({context.ParseContextName}.MaySkipWhiteSpace({cursorName}.Current))");
                result.Body.Add("{");
                result.Body.Add($"    {valueStartName} = {context.ParseContextName}.WhiteSpaceEnd();");
                result.Body.Add("}");
            }
        }

        var innerValueName = $"innerValue{context.NextNumber()}";
        var recordedName = $"recorded{context.NextNumber()}";
        // Written here rather than with ParseContext.RecordRejected(): the call takes 1.2 ns more for each rejected value
        // on .NET 10, 18.5 ns to 19.7 ns on LookaheadBenchmarks. Accepted values take the same time either way.
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
        if (skipsWhiteSpace && !context.IsCompacting)
        {
            result.Body.Add($"        var {valueStartName} = {context.ParseContextName}.SkippedWhiteSpaceEnd({startName});");
            result.Body.Add($"        if ({valueStartName} < 0)");
            result.Body.Add("        {");
            result.Body.Add($"            {context.ParseContextName}.SkipWhiteSpace();");
            result.Body.Add($"            {valueStartName} = {cursorName}.Offset;");
            result.Body.Add($"            {cursorName}.ResetPosition({startName});");
            result.Body.Add("        }");
            result.Body.Add($"        {cursorName}.RecordFailure({valueStartName});");
        }
        else
        {
            result.Body.Add($"        {cursorName}.RecordFailure({(skipsWhiteSpace ? valueStartName : startName)});");
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
