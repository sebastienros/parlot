using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Succeeds without reading anything where a parser doesn't match, and fails where it matches (negative lookahead, the PEG
/// <c>!p</c>).
/// </summary>
/// <typeparam name="T">The output parser type.</typeparam>
/// <remarks>
/// It has no <see cref="ISeekable"/> hints, since it succeeds without reading, but a sequence which starts with it has the
/// hints of its parser and of the parser which follows it. The parser isn't invoked where the current char is one it can't
/// start with.
/// </remarks>
public sealed class Not<T> : Parser<T>, ISourceable, ILoopAnalyzable, ILookahead
{
    private readonly Parser<T> _parser;

    // Whether the parser is invoked without its value, which isn't used
    private readonly bool _withoutValue;

    // Whether the parser can start at the current position: it would fail where it can't. A choice is guarded too: the
    // parser of a negative lookahead mostly fails.
    private readonly SeekGuard _guard;

    /// <summary>
    /// Creates a parser that succeeds where <paramref name="parser"/> doesn't match.
    /// </summary>
    /// <param name="parser">The parser to look ahead with.</param>
    public Not(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _withoutValue = parser.CanParseWithoutValue;
        _guard = new SeekGuard(parser, guardChoices: true);
    }

    /// <summary>
    /// Creates a parser failing when <paramref name="parser"/> matches, which records <paramref name="reason"/> with the
    /// failure, see <see cref="Cursor.FurthestReason"/>.
    /// </summary>
    /// <param name="parser">The parser which must not match.</param>
    /// <param name="reason">Why it must not: a constant, like a message or an <see langword="enum"/> value.</param>
    public Not(Parser<T> parser, object reason) : this(parser)
    {
        _reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    // Recorded with the failure when the parser matches, null when there is none
    private readonly object? _reason;

    object ILookahead.Parser => _parser;

    bool ILookahead.MatchesParser => false;

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        return true;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;
        var recorded = cursor.RecordedFailure;

        // The text is read again when the parser fails. The guard reads the char after the white space the parser skips.
        var pin = context.Pin();
        var success = _guard.CanStart(context) && (_withoutValue ? _parser.ParseWithoutValue(context, ref result) : _parser.Parse(context, ref result));
        context.Unpin(pin);

        if (!success)
        {
            // What it read, to find that it doesn't match, says nothing about what follows
            cursor.RestoreRecordedFailure(recorded);

            // Nothing is read: the result is empty, where the parser was invoked, and doesn't keep what the
            // inner parser could have written before failing
            result.Set(start, start, default!);

            context.ExitParser(this);
            return true;
        }

        // The failure is at the start of what matched, wherever it was read up to
        context.RecordRejected(start, recorded, _parser is ISeekable { SkipWhitespace: true }, _reason);
        cursor.ResetPosition(start);

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

        // Use helper instead of inlining. The parser isn't invoked where it can't start.
        var helperName = context.WithDiscardResult(true, () => SeekGuard.GetHelper(context, _parser, context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Not", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName, typeof(T), guardChoices: true));

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
        // A reason is a constant, recorded with the failure
        var reason = context.ReasonArgument(_reason);
        result.Body.Add($"    {context.ParseContextName}.RecordRejected({startName}, {recordedName}, {(_parser is ISeekable { SkipWhitespace: true } ? "true" : "false")}{reason});");
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

    public override string ToString() => FormatOnce($"Not ({_parser})");
}
