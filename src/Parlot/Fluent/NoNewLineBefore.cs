using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Skips the white space and comments before a parser, and fails when they contain a new line: JavaScript's
/// "[no LineTerminator here]".
/// </summary>
/// <remarks>
/// <para>
/// The white space is skipped with <see cref="ParseContext.SkipWhiteSpace"/>, so it includes the comments of
/// <see cref="ParseContext.WhiteSpaceParser"/>, and a multi-line comment containing a new line counts as one.
/// </para>
/// <para>
/// It has the <see cref="ISeekable"/> hints of its parser, and skips white space, so a choice holding it with alternatives
/// which skip white space keeps its lookup table, and skips the white space once before it invokes them. When one of them
/// can start with this parser, the choice tells it the white space it skipped, where this parser looks for the new lines.
/// So does <c>Operators()</c>, whose operators are tried through a choice. When an alternative can start with a parser which
/// skips the white space before this one, the choice selects them with the char after the white space and invokes them
/// before it instead, and so does <c>Longest</c>, which tries all its alternatives then.
/// </para>
/// </remarks>
public sealed class NoNewLineBefore<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ILeadingWhiteSpace, IReadsLeadingWhiteSpace
{
    private readonly string? _newLines;

    // Whether the parser is invoked without its value when this one's isn't used
    private readonly bool _withoutValue;

    /// <summary>
    /// Creates a parser that fails when the white space before <paramref name="parser"/> contains a new line.
    /// </summary>
    /// <param name="parser">The parser to invoke after the white space.</param>
    /// <param name="newLines">The chars that are new lines, or <see langword="null"/> for the new lines of <see cref="ParseContext.WhiteSpaceSet"/>.</param>
    public NoNewLineBefore(Parser<T> parser, string? newLines = null)
    {
        Parser = parser ?? throw new ArgumentNullException(nameof(parser));

        if (newLines is { Length: 0 })
        {
            throw new ArgumentException("At least one new line char is required.", nameof(newLines));
        }

        _newLines = newLines;
        _withoutValue = parser.CanParseWithoutValue;

        LeadingWhiteSpace.RegisterReader();
    }

    /// <summary>
    /// The parser invoked after the white space.
    /// </summary>
    public Parser<T> Parser { get; }

    object IReadsLeadingWhiteSpace.Parser => Parser;

    string? IReadsLeadingWhiteSpace.NewLines => _newLines;

    // The hints of the parser it invokes after the white space, read when they are asked for since it can be a Deferred set
    // later. It skips the white space itself, whether its parser does or not.
    private ForwardedHints? _hints;

    /// <inheritdoc />
    public bool CanSeek => ForwardedHints.Get(ref _hints, Parser).CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, Parser).ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => true;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(Parser);

    // Its match depends on the white space: a parser which needs where its value starts reads it without skipping it before,
    // and a choice invokes it before the white space, see IReadsLeadingWhiteSpace
    LeadingWhiteSpaceUse ILeadingWhiteSpace.LeadingWhiteSpace => LeadingWhiteSpaceUse.Kept;

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(Parser);

    /// <inheritdoc />
    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        if (!context.SkipWhiteSpaceAndFindNewLineBefore(_newLines) && Parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            return true;
        }

        cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    internal override bool CanParseWithoutValue => _withoutValue;

    // Parse, with the parser invoked without its value
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        if (!context.SkipWhiteSpaceAndFindNewLineBefore(_newLines) && (_withoutValue ? Parser.ParseWithoutValue(context, ref result) : Parser.Parse(context, ref result)))
        {
            context.ExitParser(this);
            return true;
        }

        cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    /// <inheritdoc />
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (Parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("NoNewLineBefore requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;

        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_NoNewLine", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // var start = cursor.Offset;
        // success = !context.SkipWhiteSpaceAndFindNewLineBefore(newLines) && Helper(context, out value);
        // if (!success) cursor.ResetPosition(start);
        var startName = $"start{context.NextNumber()}";
        var newLines = _newLines is null ? "null" : LiteralHelper.StringToLiteral(_newLines);
        var output = context.DiscardResult ? "_" : result.ValueVariable;

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = !{ctx}.SkipWhiteSpaceAndFindNewLineBefore({newLines}) && {helperName}({ctx}, out {output});");
        result.Body.Add($"if (!{result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Parser} (No new line before)";
}
