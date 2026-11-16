using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

/// <summary>
/// Reports the failures of a parser where it starts, and where it ends when it matches, for the furthest failure: the
/// parser is a token, like the ones a lexer reads.
/// </summary>
/// <typeparam name="T">The output parser type.</typeparam>
/// <remarks>
/// <para>
/// A token built from <c>Literals</c> tries continuations it drops, which move the cursor back and count toward
/// <see cref="Cursor.FurthestOffset"/>: a dotted name tries a name after each <c>.</c>. This parser forgets what its
/// parser recorded. When the parser fails, the furthest failure is where it starts, after the white space it skips first;
/// when it matches, where it ends, and what follows reports its own failures.
/// </para>
/// <para>
/// It matches what its parser matches, with its value. The white space before the token is the one of where it is
/// invoked: a white space parser or set for the token goes around it, as in <c>AsToken(parser).WithComments(...)</c>.
/// </para>
/// </remarks>
public sealed class AsToken<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, IReadsWith, IInvokesOnlyLiterals
{
    private readonly Parser<T> _parser;

    // Whether the parser is invoked without its value when the value of this parser isn't used
    private readonly bool _withoutValue;

    // The hints of the parser, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    /// <summary>
    /// Creates a parser which reports the failures of <paramref name="parser"/> where it starts.
    /// </summary>
    /// <param name="parser">The parser of the token.</param>
    public AsToken(Parser<T> parser)
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

    // It reads only where the parser does, see CustomWhiteSpace
    object IReadsWith.Parser => _parser;

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    // When the parser it wraps is a literal, see IInvokesOnlyLiterals
    bool IInvokesOnlyLiterals.InvokesOnlyLiterals => InvokesOnlyLiterals.Of(_parser);

    /// <inheritdoc />
    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        // A compacting buffer can't be read again from the start once the parser failed: where the token starts is read
        // first, as a When does
        var tokenStart = context.IsCompacting ? ReadTokenStart(context, start) : -1;
        var recorded = cursor.RecordedFailure;

        var success = _parser.Parse(context, ref result);

        AfterParse(context, success, start, tokenStart, recorded);

        context.ExitParser(this);
        return success;
    }

    internal override bool CanParseWithoutValue => _withoutValue;

    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;
        var tokenStart = context.IsCompacting ? ReadTokenStart(context, start) : -1;
        var recorded = cursor.RecordedFailure;

        var success = _parser.ParseWithoutValue(context, ref result);

        AfterParse(context, success, start, tokenStart, recorded);

        context.ExitParser(this);
        return success;
    }

    // Forgets what the parser recorded. When it matched, where it ends is the cursor's offset. When it failed, the token
    // starts after the white space the parser skips first, where a token which skips it fails when it isn't found.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AfterParse(ParseContext context, bool success, int start, int tokenStart, int recorded)
    {
        var cursor = context.Scanner.Cursor;

        if (success)
        {
            cursor.RestoreRecordedFailure(recorded);
        }
        else if (tokenStart >= 0)
        {
            // Compacting: the white space was skipped before the parser, which then moved the cursor back to after it
            cursor.ResetPosition(start);
            cursor.RestoreRecordedFailure(recorded);
            cursor.RecordFailure(tokenStart);
        }
        else
        {
            cursor.RestoreRecordedFailure(recorded);

            // The char is checked first: most tokens have no white space before them, which the parser that invokes them
            // often skipped already. The hint is the parser's, kept until the grammar changes.
            if (context.MaySkipWhiteSpace(cursor.Current) && SkipWhitespace)
            {
                cursor.RecordFailure(context.WhiteSpaceEnd());
            }
        }
    }

    // What the parser does with the white space before it, read when it is first asked for since the parser can be a
    // Deferred set later
    private LeadingWhiteSpace.Cache? _leading;

    // Where the token starts when the buffer is a compacting one: after the white space the parser skips, or where it is
    // invoked when it doesn't skip any. Not inlined: parsing a string doesn't pay for it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ReadTokenStart(ParseContext context, int start)
    {
        if (!context.MaySkipWhiteSpace(context.Scanner.Cursor.Current))
        {
            return start;
        }

        var leading = LeadingWhiteSpace.Of(ref _leading, _parser);

        if (leading == LeadingWhiteSpaceUse.None)
        {
            return start;
        }

        if (leading == LeadingWhiteSpaceUse.Skipped)
        {
            // The parser skips it before anything else, like a token, a sequence or a choice of tokens: it is skipped
            // for it
            context.SkipWhiteSpace();

            return context.Scanner.Cursor.Offset;
        }

        // A capture holds the white space its parser skips, and a lookahead leaves the cursor before it
        return context.WhiteSpaceEnd();
    }

    /// <inheritdoc />
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("AsToken requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var recordedName = $"recorded{context.NextNumber()}";
        var skipsWhiteSpace = SkipWhitespace;

        // The helper keeps the value only when it is used, like the result of this parser
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_AsToken", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        var call = context.DiscardResult
            ? $"{helperName}({ctx}, out _)"
            : $"{helperName}({ctx}, out {result.ValueVariable})";

        if (skipsWhiteSpace && context.IsCompacting)
        {
            // A compacting buffer can't be read again from the start once the parser failed: where the token starts is
            // read first. The white space is skipped for a parser which skips it first itself, and read without moving
            // the cursor for one which reads from where it is invoked, like a capture or a lookahead.
            var startName = $"start{context.NextNumber()}";
            var tokenStartName = $"tokenStart{context.NextNumber()}";

            result.Body.Add($"var {startName} = {cursorName}.Offset;");
            result.Body.Add($"var {tokenStartName} = {startName};");
            result.Body.Add($"if ({ctx}.MaySkipWhiteSpace({cursorName}.Current))");
            result.Body.Add("{");

            if (LeadingWhiteSpace.CanSkipBefore(_parser))
            {
                result.Body.Add($"    {ctx}.SkipWhiteSpace();");
                result.Body.Add($"    {tokenStartName} = {cursorName}.Offset;");
            }
            else
            {
                result.Body.Add($"    {tokenStartName} = {ctx}.WhiteSpaceEnd();");
            }

            result.Body.Add("}");
            result.Body.Add($"var {recordedName} = {cursorName}.RecordedFailure;");
            result.Body.Add($"{result.SuccessVariable} = {call};");
            result.Body.Add($"if ({result.SuccessVariable})");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.RestoreRecordedFailure({recordedName});");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add($"    {cursorName}.RestoreRecordedFailure({recordedName});");
            result.Body.Add($"    {cursorName}.RecordFailure({tokenStartName});");
            result.Body.Add("}");

            return result;
        }

        // var recorded = cursor.RecordedFailure;
        // success = Helper(context, out value);
        // cursor.RestoreRecordedFailure(recorded);
        // // Only when the parser skips white space first
        // if (!success && context.MaySkipWhiteSpace(cursor.Current))
        // {
        //     cursor.RecordFailure(context.WhiteSpaceEnd());
        // }
        result.Body.Add($"var {recordedName} = {cursorName}.RecordedFailure;");
        result.Body.Add($"{result.SuccessVariable} = {call};");
        result.Body.Add($"{cursorName}.RestoreRecordedFailure({recordedName});");

        if (skipsWhiteSpace)
        {
            result.Body.Add($"if (!{result.SuccessVariable} && {ctx}.MaySkipWhiteSpace({cursorName}.Current))");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.RecordFailure({ctx}.WhiteSpaceEnd());");
            result.Body.Add("}");
        }

        return result;
    }

    /// <inheritdoc />
    public override string ToString() => $"AsToken ({_parser})";
}
