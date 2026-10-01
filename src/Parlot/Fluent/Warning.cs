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
public sealed class Warning<T> : Parser<T>, ISeekable, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly string _message;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public Warning(Parser<T> parser, string message)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _message = message ?? throw new ArgumentNullException(nameof(message));

        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Position;

        if (!_parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            return false;
        }

        context.ExitParser(this);

        if (!context.CollectDiagnostics && !context.TreatWarningsAsErrors)
        {
            return true;
        }

        var location = start;
        if (SkipWhitespace)
        {
            var end = cursor.Position;
            cursor.ResetPosition(start);
            context.SkipWhiteSpace();
            location = cursor.Position;
            cursor.ResetPosition(end);
        }

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

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";
        var locationName = $"location{context.NextNumber()}";
        var message = LiteralHelper.StringToLiteral(_message);
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Warning", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        var outTarget = context.DiscardResult ? "_" : result.ValueVariable;

        result.Body.Add($"var {startName} = {cursorName}.Position;");
        result.Body.Add($"{result.SuccessVariable} = {helperName}({ctx}, out {outTarget});");
        result.Body.Add($"if ({result.SuccessVariable} && ({ctx}.CollectDiagnostics || {ctx}.TreatWarningsAsErrors))");
        result.Body.Add("{");
        result.Body.Add($"    var {locationName} = {startName};");
        if (SkipWhitespace)
        {
            result.Body.Add($"    var {endName} = {cursorName}.Position;");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add($"    {ctx}.SkipWhiteSpace();");
            result.Body.Add($"    {locationName} = {cursorName}.Position;");
            result.Body.Add($"    {cursorName}.ResetPosition({endName});");
        }
        result.Body.Add($"    if (!{ctx}.TreatWarningsAsErrors)");
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

    public override string ToString() => $"{_parser} (Warning)";
}
