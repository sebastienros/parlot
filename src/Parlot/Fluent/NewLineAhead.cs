using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Succeeds when the white space and comments at the cursor contain a new line, without consuming them, returning
/// <see langword="null"/>: the line break of JavaScript's automatic semicolon insertion.
/// </summary>
/// <remarks>
/// The white space is the one <see cref="ParseContext.SkipWhiteSpace"/> skips, so it includes the comments of
/// <see cref="ParseContext.WhiteSpaceParser"/>, and a multi-line comment containing a new line counts as one. The skipped
/// white space is cached by the context, so the parser that reads the next token doesn't skip it again.
/// </remarks>
public sealed class NewLineAhead : Parser<object?>, ISourceable, ILoopAnalyzable
{
    private readonly string? _newLines;

    /// <summary>
    /// Creates a parser that succeeds when the white space at the cursor contains a new line.
    /// </summary>
    /// <param name="newLines">The chars that are new lines, or <see langword="null"/> for the new lines of <see cref="ParseContext.WhiteSpaceSet"/>.</param>
    public NewLineAhead(string? newLines = null)
    {
        if (newLines is { Length: 0 })
        {
            throw new ArgumentException("At least one new line char is required.", nameof(newLines));
        }

        _newLines = newLines;
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => true;

    /// <inheritdoc />
    public override bool Parse(ParseContext context, ref ParseResult<object?> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;
        var recorded = cursor.RecordedFailure;

        var found = context.SkipWhiteSpaceAndFindNewLine(_newLines);

        // A lookahead: the white space is read again by the next parser, and what was read says nothing of a failure
        cursor.ResetPosition(start);
        cursor.RestoreRecordedFailure(recorded);

        if (found)
        {
            result.Set(start, start, null);
        }

        context.ExitParser(this);
        return found;
    }

    /// <inheritdoc />
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(object));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var recordedName = $"recorded{context.NextNumber()}";
        var newLines = _newLines is null ? "null" : LiteralHelper.StringToLiteral(_newLines);

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"var {recordedName} = {cursorName}.RecordedFailure;");
        result.Body.Add($"{result.SuccessVariable} = {context.ParseContextName}.SkipWhiteSpaceAndFindNewLine({newLines});");
        result.Body.Add($"{cursorName}.ResetPosition({startName});");
        result.Body.Add($"{cursorName}.RestoreRecordedFailure({recordedName});");

        if (!context.DiscardResult)
        {
            result.Body.Add($"{result.ValueVariable} = null;");
        }

        return result;
    }

    /// <inheritdoc />
    public override string ToString() => "NewLineAhead";
}
