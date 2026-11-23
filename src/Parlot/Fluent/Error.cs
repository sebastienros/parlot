using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

// Not an ISeekable: a choice would skip it for the chars its parser doesn't start with, where it reports an error instead of
// failing
public sealed class ElseError<T> : Parser<T>, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;
    private readonly string _message;
    private readonly bool _withoutValue;

    public ElseError(Parser<T> parser, string message)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _message = message ?? throw new ArgumentNullException(nameof(message));
        _withoutValue = _parser.CanParseWithoutValue;
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        if (context.IsCompacting)
        {
            return ParseCompacting(context, ref result, withValue: true);
        }

        context.EnterParser(this);

        if (!_parser.Parse(context, ref result))
        {
            context.ExitParser(this);

            if (context.CollectDiagnostics)
            {
                context.AddDiagnostic(_message, context.Scanner.Cursor.Position, ParseDiagnosticSeverity.Error);
                return false;
            }

            throw new ParseException(_message, context.Scanner.Cursor.Position);
        }

        context.ExitParser(this);
        return true;
    }

    internal override bool CanParseWithoutValue => _withoutValue;

    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result)
    {
        if (context.IsCompacting)
        {
            return ParseCompacting(context, ref result, withValue: false);
        }

        context.EnterParser(this);

        if (!(_withoutValue ? _parser.ParseWithoutValue(context, ref result) : _parser.Parse(context, ref result)))
        {
            context.ExitParser(this);

            if (context.CollectDiagnostics)
            {
                context.AddDiagnostic(_message, context.Scanner.Cursor.Position, ParseDiagnosticSeverity.Error);
                return false;
            }

            throw new ParseException(_message, context.Scanner.Cursor.Position);
        }

        context.ExitParser(this);
        return true;
    }

    private bool ParseCompacting(ParseContext context, ref ParseResult<T> result, bool withValue)
    {
        context.EnterParser(this);

        // A parser which fails moves the cursor back to its start, whose text might have been discarded from the buffer by then.
        // The line and column are computed from the text, so the position is read while the text is buffered.
        var cursor = context.Scanner.Cursor;
        var start = cursor.Position;

        if (!(withValue || !_withoutValue ? _parser.Parse(context, ref result) : _parser.ParseWithoutValue(context, ref result)))
        {
            context.ExitParser(this);

            var position = cursor.Offset == start.Offset ? start : cursor.Position;

            if (context.CollectDiagnostics)
            {
                context.AddDiagnostic(_message, position, ParseDiagnosticSeverity.Error);
                return false;
            }

            throw new ParseException(_message, position);
        }

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("ElseError requires a source-generatable parser.");
        }

        // The code collecting the diagnostics leaves the parser failed
        var result = context.CreateResult(typeof(T), defaultSuccess: !context.CollectDiagnostics);
        var cursorName = context.CursorName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_ElseError", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out value))
        // {
        //     success = true;
        // }
        // else
        // {
        //     throw new ParseException(_message, cursor.Position);
        // }

        string? startName = null;

        if (context.IsCompacting)
        {
            // A parser which fails moves the cursor back to its start, whose text might have been discarded from the buffer by then.
            // The line and column are computed from the text, so the position is read while the text is buffered.
            startName = $"start{context.NextNumber()}";
            result.Body.Add($"var {startName} = {cursorName}.Position;");
        }

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out _))");
        }
        else
        {
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out {result.ValueVariable}))");
        }
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");

        if (startName != null)
        {
            var positionName = $"position{context.NextNumber()}";
            result.Body.Add($"    var {positionName} = {cursorName}.Offset == {startName}.Offset ? {startName} : {cursorName}.Position;");
            ErrorSource.AddReport(result, context, _message, positionName, "    ", resetTo: null);
        }
        else
        {
            ErrorSource.AddReport(result, context, _message, $"{cursorName}.Position", "    ", resetTo: null);
        }

        result.Body.Add("}");

        return result;
    }

    public override string ToString() => FormatOnce($"{_parser} (ElseError)");
}

public sealed class Error<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;
    private readonly string _message;
    private readonly bool _withoutValue;

    // The error is only raised when the parser matches, so it can't be raised for a char it doesn't start with. The hints
    // are read when they are asked for since the parser can be a Deferred set later.
    private ForwardedHints? _hints;

    /// <inheritdoc />
    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    public Error(Parser<T> parser, string message)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _message = message ?? throw new ArgumentNullException(nameof(message));
        _withoutValue = _parser.CanParseWithoutValue;
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        // It throws when the parser succeeds
        return false;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
        => ErrorSource.ParseError(this, _parser, _withoutValue, _message, context);

    public SourceResult GenerateSource(SourceGenerationContext context)
        => ErrorSource.GenerateErrorSource(_parser, typeof(T), typeof(T), _message, context);

    public override string ToString() => FormatOnce($"{_parser} (Error)");
}

public sealed class Error<T, U> : Parser<U>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;
    private readonly string _message;
    private readonly bool _withoutValue;

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    public Error(Parser<T> parser, string message)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _message = message ?? throw new ArgumentNullException(nameof(message));
        _withoutValue = _parser.CanParseWithoutValue;
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        // It throws when the parser succeeds
        return false;
    }

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
        => ErrorSource.ParseError(this, _parser, _withoutValue, _message, context);

    public SourceResult GenerateSource(SourceGenerationContext context)
        => ErrorSource.GenerateErrorSource(_parser, typeof(T), typeof(U), _message, context);

    public override string ToString() => FormatOnce($"{_parser} (Error)");
}

internal static class ErrorSource
{
    internal static bool ParseError<TInner, TResult>(Parser<TResult> owner, Parser<TInner> inner, bool withoutValue, string message, ParseContext context)
    {
        context.EnterParser(owner);

        var parsed = new ParseResult<TInner>();
        var start = context.Scanner.Cursor.Offset;

        if (withoutValue ? inner.ParseWithoutValue(context, ref parsed) : inner.Parse(context, ref parsed))
        {
            context.ExitParser(owner);

            if (context.CollectDiagnostics)
            {
                context.AddDiagnostic(message, context.Scanner.Cursor.Position, ParseDiagnosticSeverity.Error);
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            throw new ParseException(message, context.Scanner.Cursor.Position);
        }

        context.ExitParser(owner);
        return false;
    }

    internal static SourceResult GenerateErrorSource(object inner, Type innerType, Type resultType, string message, SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (inner is not ISourceable sourceable)
        {
            throw new NotSupportedException($"Error requires a source-generatable parser, but {SourceGenerationContext.DescribeParser(inner)} does not implement ISourceable.");
        }

        var result = context.CreateResult(resultType);
        var cursorName = context.CursorName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(innerType);

        var helperName = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Error", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName);

        string? startName = null;

        if (context.CollectDiagnostics)
        {
            startName = $"start{context.NextNumber()}";
            result.Body.Add($"var {startName} = {cursorName}.Offset;");
        }

        result.Body.Add($"if ({helperName}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        AddReport(result, context, message, $"{cursorName}.Position", "    ", startName);
        result.Body.Add("}");

        return result;
    }
    // Emits the code reporting an error: it throws, or records it when the diagnostics are collected and the parser fails.
    // resetTo is the variable holding the offset the cursor is moved back to when the error is recorded.
    internal static void AddReport(SourceResult result, SourceGenerationContext context, string message, string position, string indent, string? resetTo)
    {
        var literal = LiteralHelper.StringToLiteral(message);
        var ctx = context.ParseContextName;

        if (!context.CollectDiagnostics)
        {
            result.Body.Add($"{indent}throw new global::Parlot.ParseException({literal}, {position});");
            return;
        }

        result.Body.Add($"{indent}if (!{ctx}.CollectDiagnostics)");
        result.Body.Add($"{indent}{{");
        result.Body.Add($"{indent}    throw new global::Parlot.ParseException({literal}, {position});");
        result.Body.Add($"{indent}}}");
        result.Body.Add($"{indent}{ctx}.AddDiagnostic({literal}, {position}, global::Parlot.ParseDiagnosticSeverity.Error);");

        if (resetTo != null)
        {
            result.Body.Add($"{indent}{context.CursorName}.ResetPosition({resetTo});");
        }
    }
}
