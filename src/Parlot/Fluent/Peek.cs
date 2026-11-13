using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Succeeds with the value of a parser when it matches, without consuming the text it matched (positive lookahead, the
/// PEG <c>&amp;p</c>).
/// </summary>
/// <typeparam name="T">The output parser type.</typeparam>
/// <remarks>
/// The result is empty: it starts and ends where the parser was invoked. When the parser fails, this parser fails too, and
/// the cursor is where it was.
/// </remarks>
public sealed class Peek<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;

    // Whether the parser is invoked without its value when the value of this parser isn't used
    private readonly bool _withoutValue;

    // The hints of the parser, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    /// <summary>
    /// Creates a parser that succeeds with the value of <paramref name="parser"/> when it matches, without moving the cursor.
    /// </summary>
    /// <param name="parser">The parser to look ahead with.</param>
    public Peek(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _withoutValue = parser.CanParseWithoutValue;
    }

    /// <inheritdoc />
    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    // It succeeds without reading anything, whatever its parser reads
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        return true;
    }

    /// <inheritdoc />
    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;
        var recorded = cursor.RecordedFailure;

        // The text is read again once the parser matched
        var pin = context.Pin();
        var success = _parser.Parse(context, ref result);
        context.Unpin(pin);

        if (success)
        {
            // What the parser matched says nothing about what follows at this position. When it fails, it was expected:
            // its failure counts.
            cursor.ResetPosition(start);
            cursor.RestoreRecordedFailure(recorded);
            result.Set(start, start, result.Value);
        }

        context.ExitParser(this);
        return success;
    }

    internal override bool CanParseWithoutValue => _withoutValue;

    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;
        var recorded = cursor.RecordedFailure;

        var pin = context.Pin();
        var success = _parser.ParseWithoutValue(context, ref result);
        context.Unpin(pin);

        if (success)
        {
            cursor.ResetPosition(start);
            cursor.RestoreRecordedFailure(recorded);
            result.Set(start, start, result.Value);
        }

        context.ExitParser(this);
        return success;
    }

    /// <inheritdoc />
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Peek requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var startName = $"start{context.NextNumber()}";
        var recordedName = $"recorded{context.NextNumber()}";

        // The helper keeps the value only when it is used, like the result of this parser
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Peek", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // var start = cursor.Offset;
        // var recorded = cursor.RecordedFailure;
        // if (Helper(context, out value))
        // {
        //     cursor.ResetPosition(start);
        //     // What the parser matched says nothing about what follows at this position
        //     cursor.RestoreRecordedFailure(recorded);
        //     success = true;
        // }
        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"var {recordedName} = {cursorName}.RecordedFailure;");
        var call = context.DiscardResult
            ? $"{helperName}({context.ParseContextName}, out _)"
            : $"{helperName}({context.ParseContextName}, out {result.ValueVariable})";
        var success = context.PinnedCall(result, call);
        result.Body.Add($"if ({success})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add($"    {cursorName}.RestoreRecordedFailure({recordedName});");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    /// <inheritdoc />
    public override string ToString() => $"Peek ({_parser})";
}
