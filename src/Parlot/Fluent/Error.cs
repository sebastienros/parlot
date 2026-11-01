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

    public ElseError(Parser<T> parser, string message)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _message = message ?? throw new ArgumentNullException(nameof(message));
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        if (context.IsCompacting)
        {
            return ParseCompacting(context, ref result);
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

    private bool ParseCompacting(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        // A parser which fails moves the cursor back to its start, whose text might have been discarded from the buffer by then.
        // The line and column are computed from the text, so the position is read while the text is buffered.
        var cursor = context.Scanner.Cursor;
        var start = cursor.Position;

        if (!_parser.Parse(context, ref result))
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
        ErrorSource.AddReport(result, context, _message, $"{cursorName}.Position", "    ", resetTo: null);
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (ElseError)";
}

public sealed class Error<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;
    private readonly string _message;

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
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        // It throws when the parser succeeds
        return false;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref result))
        {
            context.ExitParser(this);

            if (context.CollectDiagnostics)
            {
                context.AddDiagnostic(_message, context.Scanner.Cursor.Position, ParseDiagnosticSeverity.Error);
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            throw new ParseException(_message, context.Scanner.Cursor.Position);
        }

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Error requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Error", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out _))
        // {
        //     throw new ParseException(_message, cursor.Position);
        // }
        // success = false;

        string? startName = null;

        if (context.CollectDiagnostics)
        {
            startName = $"start{context.NextNumber()}";
            result.Body.Add($"var {startName} = {cursorName}.Offset;");
        }

        result.Body.Add($"if ({helperName}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        ErrorSource.AddReport(result, context, _message, $"{cursorName}.Position", "    ", startName);
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Error)";
}

public sealed class Error<T, U> : Parser<U>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;
    private readonly string _message;

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
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        // It throws when the parser succeeds
        return false;
    }

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();
        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref parsed))
        {
            context.ExitParser(this);

            if (context.CollectDiagnostics)
            {
                context.AddDiagnostic(_message, context.Scanner.Cursor.Position, ParseDiagnosticSeverity.Error);
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            throw new ParseException(_message, context.Scanner.Cursor.Position);
        }

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Error requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(U));
        var cursorName = context.CursorName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Error", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out _))
        // {
        //     throw new ParseException(_message, cursor.Position);
        // }
        // success = false;

        string? startName = null;

        if (context.CollectDiagnostics)
        {
            startName = $"start{context.NextNumber()}";
            result.Body.Add($"var {startName} = {cursorName}.Offset;");
        }

        result.Body.Add($"if ({helperName}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        ErrorSource.AddReport(result, context, _message, $"{cursorName}.Position", "    ", startName);
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Error)";
}

internal static class ErrorSource
{
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
