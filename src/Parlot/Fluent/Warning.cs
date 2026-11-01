using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Reports a warning when the inner parser succeeds, without changing its result.
/// </summary>
/// <remarks>
/// The warning is located where the matched input starts, after any whitespace the inner parser skipped.
/// It is only recorded when <see cref="ParseContext.CollectDiagnostics"/> is enabled. When
/// <see cref="ParseContext.TreatWarningsAsErrors"/> is enabled it is reported as an error instead, and this parser fails.
/// </remarks>
public sealed class Warning<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<T> _parser;
    private readonly string _message;

    // Whether the parser skipped white space when this one was built, which is where the warning is located
    private readonly bool _skipWhitespace;

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    /// <inheritdoc />
    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    public Warning(Parser<T> parser, string message)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _message = message ?? throw new ArgumentNullException(nameof(message));

        _skipWhitespace = _parser is ISeekable { SkipWhitespace: true };
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (!context.CollectDiagnostics && !context.TreatWarningsAsErrors)
        {
            var success = _parser.Parse(context, ref result);

            context.ExitParser(this);
            return success;
        }

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        // The warning is located after the white space the parser skips. It's skipped first, and the position read before
        // the parser runs, since a compacting buffer can discard the text the parser reads.
        if (_skipWhitespace)
        {
            context.SkipWhiteSpace();
        }

        var location = cursor.Position;

        if (!_parser.Parse(context, ref result))
        {
            cursor.ResetPosition(start);

            context.ExitParser(this);
            return false;
        }

        context.ExitParser(this);

        if (!context.TreatWarningsAsErrors)
        {
            context.AddDiagnostic(_message, location, ParseDiagnosticSeverity.Warning);
            return true;
        }

        if (!context.CollectDiagnostics)
        {
            throw new ParseException(_message, location);
        }

        context.AddDiagnostic(_message, location, ParseDiagnosticSeverity.Error);
        cursor.ResetPosition(start);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Warning requires a source-generatable parser.");
        }

        // Generated parsers only report warnings through the entry points returning the diagnostics
        if (!context.CollectDiagnostics)
        {
            return sourceable.GenerateSource(context);
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var reportName = $"report{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";
        var locationName = $"location{context.NextNumber()}";
        var message = LiteralHelper.StringToLiteral(_message);
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Warning", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        var outTarget = context.DiscardResult ? "_" : result.ValueVariable;

        result.Body.Add($"var {reportName} = {ctx}.CollectDiagnostics || {ctx}.TreatWarningsAsErrors;");
        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"var {locationName} = default(global::Parlot.TextPosition);");
        result.Body.Add($"if ({reportName})");
        result.Body.Add("{");
        if (_skipWhitespace)
        {
            result.Body.Add($"    {ctx}.SkipWhiteSpace();");
        }
        result.Body.Add($"    {locationName} = {cursorName}.Position;");
        result.Body.Add("}");
        result.Body.Add($"{result.SuccessVariable} = {helperName}({ctx}, out {outTarget});");
        result.Body.Add($"if ({reportName})");
        result.Body.Add("{");
        result.Body.Add($"    if (!{result.SuccessVariable})");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add($"    else if (!{ctx}.TreatWarningsAsErrors)");
        result.Body.Add("    {");
        result.Body.Add($"        {ctx}.AddDiagnostic({message}, {locationName}, global::Parlot.ParseDiagnosticSeverity.Warning);");
        result.Body.Add("    }");
        result.Body.Add($"    else if (!{ctx}.CollectDiagnostics)");
        result.Body.Add("    {");
        result.Body.Add($"        throw new global::Parlot.ParseException({message}, {locationName});");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {ctx}.AddDiagnostic({message}, {locationName}, global::Parlot.ParseDiagnosticSeverity.Error);");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add($"        {result.SuccessVariable} = false;");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    // Without diagnostics the warning is the parser it wraps
    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context)
        => context.CollectDiagnostics ? -1 : SpanSource.GetWeight(_parser, context);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => writer.Generate(_parser, valueVariable, fail);

    public override string ToString() => $"{_parser} (Warning)";
}
