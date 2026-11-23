using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Returns the text of a parser as a string taken from a pool, such that a value the parser matches many times is
/// allocated once.
/// </summary>
/// <remarks>
/// <para>
/// The pool belongs to this parser and lives as long as it does: it is shared by all the parses, including concurrent
/// ones. Finding a value takes no lock, adding one does. It is bounded: it holds up to 4096 values of up to 64 chars,
/// longer values are created as usual, and it switches itself off when the values rarely repeat.
/// </para>
/// <para>
/// A lookup costs about as much as creating a short string: pooling pays off when the strings are kept, like the
/// identifiers or member names of a syntax tree held in memory, rather than when each one is used and dropped.
/// </para>
/// <para>
/// A string literal (<c>Terms.String()</c> or <c>Literals.String()</c>) also decodes its escape sequences into the
/// pool, without creating a string when the pool has the value. One built with <c>returnDecoded: false</c> pools the
/// text between its quotes as it is.
/// </para>
/// </remarks>
public sealed class PooledString : Parser<string>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable, IInvokesOnlyLiterals
{
    private readonly Parser<TextSpan> _parser;
    private readonly StringPool _pool = new();

    // Whether the parser is invoked without its value when this one's isn't used
    private readonly bool _withoutValue;

    // Whether the parser is a StringLiteral that decodes escapes directly into _pool
    private readonly bool _hasDecodedStringLiteral;

    /// <summary>
    /// Creates a parser returning the text of <paramref name="parser"/> as a pooled string.
    /// </summary>
    /// <param name="parser">The parser whose text is returned.</param>
    public PooledString(Parser<TextSpan> parser)
    {
        ThrowHelper.ThrowIfNull(parser, nameof(parser));

        // A string literal decodes its escape sequences into the pool too, rather than into a new string. It is copied,
        // since it can be used elsewhere.
        switch (parser)
        {
            case StringLiteral { ReturnDecoded: true } literal:
                _parser = new StringLiteral(literal, _pool);
                _hasDecodedStringLiteral = true;
                break;
            case SkipWhiteSpace<TextSpan> { Parser: StringLiteral { ReturnDecoded: true } literal } skip:
                _parser = new SkipWhiteSpace<TextSpan>(new StringLiteral(literal, _pool)) { Name = skip.Name };
                _hasDecodedStringLiteral = true;
                break;
            default:
                _parser = parser;
                break;
        }

        _withoutValue = _parser.CanParseWithoutValue;
    }

    // The hints of the parser, the only one this one invokes, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    /// <inheritdoc />
    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    // When the parser it wraps is a literal, see IInvokesOnlyLiterals
    bool IInvokesOnlyLiterals.InvokesOnlyLiterals => InvokesOnlyLiterals.Of(_parser);

    /// <inheritdoc />
    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<TextSpan>();

        if (_parser.Parse(context, ref parsed))
        {
            // When StringLiteral decoded escapes into _pool, it returns new TextSpan(decoded) with Offset == 0 and
            // Length == Buffer.Length; unescaped slices always start after the opening quote (Offset >= 1).
            var value = _hasDecodedStringLiteral && parsed.Value.Offset == 0 && parsed.Value.Buffer is { } buffer && parsed.Value.Length == buffer.Length
                ? buffer
                : _pool.Get(parsed.Value);

            result.Set(parsed.Start, parsed.End, value);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    internal override bool CanParseWithoutValue => true;

    // Parse, without the string: the pool isn't read, and the parser is invoked without its value
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<TextSpan>();

        if (_withoutValue ? _parser.ParseWithoutValue(context, ref parsed) : _parser.Parse(context, ref parsed))
        {
            result.Start = parsed.Start;
            result.End = parsed.End;

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    /// <inheritdoc />
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("PooledString requires a source-generatable parser.");
        }

        if (SpanSource.TryGenerate(this, typeof(string), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(string));
        var ctx = context.ParseContextName;
        var parsedName = $"parsed{context.NextNumber()}";
        var parsedTypeName = SourceGenerationContext.GetTypeName(typeof(TextSpan));

        // The inner value is only read for a result that is used
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_PooledString_{context.NextNumber()}", parsedTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperName}({ctx}, out _))");
            result.Body.Add("{");
        }
        else
        {
            result.Body.Add($"if ({helperName}({ctx}, out var {parsedName}))");
            result.Body.Add("{");
            if (_hasDecodedStringLiteral)
            {
                result.Body.Add($"    {result.ValueVariable} = {parsedName}.Offset == 0 && {parsedName}.Buffer is not null && {parsedName}.Length == {parsedName}.Buffer.Length ? {parsedName}.Buffer : {context.GetStringPoolField(_pool)}.Get({parsedName});");
            }
            else
            {
                result.Body.Add($"    {result.ValueVariable} = {context.GetStringPoolField(_pool)}.Get({parsedName});");
            }
        }

        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        if (valueVariable == null)
        {
            writer.Generate(_parser, null, fail);
            return;
        }

        var parsed = writer.DeclareLocal(typeof(TextSpan), "parsed");

        writer.Generate(_parser, parsed, fail);
        writer.Add($"{valueVariable} = {writer.Context.GetStringPoolField(_pool)}.Get({parsed});");
    }

    /// <inheritdoc />
    public override string ToString() => FormatOnce($"{_parser} (PooledString)");
}
