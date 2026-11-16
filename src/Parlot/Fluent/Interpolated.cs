using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace Parlot.Fluent;

/// <summary>
/// The options of an interpolated text, see <see cref="LiteralBuilder.Interpolated{T}(string, string, string, string, Parser{T}, InterpolationOptions, char)"/>.
/// </summary>
[Flags]
public enum InterpolationOptions
{
    /// <summary>
    /// A chunk can hold new lines, and the white space before the closing delimiter of a hole is skipped.
    /// </summary>
    None = 0,

    /// <summary>
    /// A chunk can't hold a new line (<c>'\n'</c> or <c>'\r'</c>), even after the escape char: the text fails there.
    /// </summary>
    NoNewLines = 1 << 0,

    /// <summary>
    /// The closing delimiter of a hole must follow its value, without the white space a <see cref="Parsers.Terms"/> parser skips.
    /// </summary>
    NoWhiteSpaceBeforeHoleClose = 1 << 1,
}

/// <summary>
/// An interpolated text, like a template literal: a delimited text with escapes and holes holding values, like
/// <c>`a${x}b`</c> in JavaScript or <c>$"a{x}b"</c> in C#.
/// </summary>
/// <remarks>
/// <para>
/// The text is read in chunks, up to the closing delimiter or the opening delimiter of a hole which doesn't follow the
/// escape char. An escape char skips the char after it, whatever it is: the chunks are the raw text, and decoding their
/// escape sequences is left to the grammar. A hole is read with the hole parser, which can hold interpolated texts too,
/// then with its closing delimiter.
/// </para>
/// <para>
/// The parser fails, with the cursor where it was, when the text or a hole isn't closed, or when a chunk holds a new line
/// with <see cref="InterpolationOptions.NoNewLines"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the values of the holes.</typeparam>
public sealed class Interpolated<T> : Parser<Interpolation<T>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _hole;
    private readonly Parser<string> _holeClose;
    private readonly TextLiteral _openText;
    private readonly InterpolationChunk _chunk;
    private readonly bool _holeWithoutValue;

    /// <summary>
    /// Creates an interpolated text.
    /// </summary>
    /// <param name="open">The opening delimiter of the text, like <c>"`"</c>.</param>
    /// <param name="close">The closing delimiter of the text, like <c>"`"</c>.</param>
    /// <param name="holeOpen">The opening delimiter of a hole, like <c>"${"</c>.</param>
    /// <param name="holeClose">The closing delimiter of a hole, like <c>"}"</c>.</param>
    /// <param name="hole">The parser of the value of a hole.</param>
    /// <param name="options">Whether a chunk can hold new lines, and whether white space is skipped before <paramref name="holeClose"/>.</param>
    /// <param name="escape">The escape char, which skips the char after it, or <c>'\0'</c> for none.</param>
    public Interpolated(string open, string close, string holeOpen, string holeClose, Parser<T> hole, InterpolationOptions options = InterpolationOptions.None, char escape = '\\')
    {
        ThrowIfEmpty(open, nameof(open));
        ThrowIfEmpty(close, nameof(close));
        ThrowIfEmpty(holeOpen, nameof(holeOpen));
        ThrowIfEmpty(holeClose, nameof(holeClose));

        ThrowHelper.ThrowIfNull(hole, nameof(hole));
        _hole = hole;

        if (close.StartsWith(holeOpen, StringComparison.Ordinal) || holeOpen.StartsWith(close, StringComparison.Ordinal))
        {
            throw new ArgumentException("The closing delimiter and the opening delimiter of a hole can't start with one another.", nameof(holeOpen));
        }

        if (escape != '\0' && (close[0] == escape || holeOpen[0] == escape))
        {
            throw new ArgumentException("The closing delimiter and the opening delimiter of a hole can't start with the escape char.", nameof(escape));
        }

        // A space or a tab, which every white space set skips: the parser can't tell the chars of the context, which can
        // skip others, or comments
        if ((options & InterpolationOptions.NoWhiteSpaceBeforeHoleClose) == 0 && holeClose[0] is ' ' or '\t')
        {
            throw new ArgumentException("The closing delimiter of a hole can't start with white space, which is skipped before it.", nameof(holeClose));
        }

        if ((options & InterpolationOptions.NoNewLines) != 0 && escape is '\n' or '\r')
        {
            throw new ArgumentException("The escape char can't be a new line when a chunk can't hold one.", nameof(escape));
        }

        _openText = new TextLiteral(open, StringComparison.Ordinal);

        var literal = new TextLiteral(holeClose, StringComparison.Ordinal);
        _holeClose = (options & InterpolationOptions.NoWhiteSpaceBeforeHoleClose) != 0 ? literal : new SkipWhiteSpace<string>(literal);

        _chunk = new InterpolationChunk(close, holeOpen, escape, (options & InterpolationOptions.NoNewLines) == 0);

        _holeWithoutValue = hole.CanParseWithoutValue;
    }

    private static void ThrowIfEmpty(string value, string name)
    {
        ThrowHelper.ThrowIfNull(value, name);

        if (value.Length == 0)
        {
            throw new ArgumentException("The delimiter can't be empty.", name);
        }
    }

    /// <summary>
    /// Gets the parser of the value of a hole.
    /// </summary>
    public Parser<T> Hole => _hole;

    // The hints of the opening text, which is read first
    public bool CanSeek => _openText.CanSeek;

    public char[] ExpectedChars => _openText.ExpectedChars;

    public bool SkipWhitespace => false;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_openText);

    // The opening delimiter, which isn't empty, is read before the holes are invoked. When parsing a stream, it is read by
    // a text literal, which can't succeed without reading.
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_openText);

        return false;
    }

    public override bool Parse(ParseContext context, ref ParseResult<Interpolation<T>> result) => Parse(context, ref result, withValue: true);

    internal override bool CanParseWithoutValue => true;

    // Parse, without the lists of the value, and with the holes invoked without their values when they can
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<Interpolation<T>> result) => Parse(context, ref result, withValue: false);

    private bool Parse(ParseContext context, ref ParseResult<Interpolation<T>> result, bool withValue)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        // The opening text is a token when parsing a stream, like the chunks; the holes aren't: they can be as large as the
        // text. It is read by its literal whatever the input, so that the same parsers are entered.
        var parsedOpen = new ParseResult<string>();

        if (!_openText.Parse(context, ref parsedOpen))
        {
            context.ExitParser(this);
            return false;
        }

        var chunkStart = cursor.Offset;
        var parsedChunk = new ParseResult<int>();

        if (ReadChunk(context, ref parsedChunk))
        {
            var chunk = parsedChunk.Value;

            // Most texts have no hole: their value needs no list
            if ((chunk & 1) == InterpolationScanner.Close)
            {
                result.Set(start, cursor.Offset, withValue ? new Interpolation<T>(cursor.CreateSpan(chunkStart, chunk >> 1)) : default);

                context.ExitParser(this);
                return true;
            }

            if (ParseHoles(context, withValue ? cursor.CreateSpan(chunkStart, chunk >> 1) : default, withValue, out var value))
            {
                result.Set(start, cursor.Offset, value);

                context.ExitParser(this);
                return true;
            }
        }

        cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    // Reads the holes and the chunks after them, from the first hole, which follows the chunk given. Without the value, the
    // holes are invoked without their values when they can, and nothing is listed.
    private bool ParseHoles(ParseContext context, TextSpan chunk, bool withValue, out Interpolation<T> value)
    {
        var cursor = context.Scanner.Cursor;

        // A single list, of each hole with the chunk before it: two lists filled in turn would share the stack of the thread
        // when the holes are TextSpans too, which only nested lists can
        var parts = new ListBuilder<(TextSpan Chunk, T Hole)>();

        try
        {
            var parsedHole = new ParseResult<T>();
            var parsedClose = new ParseResult<string>();
            var parsedChunk = new ParseResult<int>();

            while (true)
            {
                if (!(withValue || !_holeWithoutValue ? _hole.Parse(context, ref parsedHole) : _hole.ParseWithoutValue(context, ref parsedHole))
                    || !_holeClose.Parse(context, ref parsedClose))
                {
                    break;
                }

                if (withValue)
                {
                    parts.Add((chunk, parsedHole.Value));
                }

                var chunkStart = cursor.Offset;

                if (!ReadChunk(context, ref parsedChunk))
                {
                    break;
                }

                if ((parsedChunk.Value & 1) == InterpolationScanner.Close)
                {
                    value = withValue ? Interpolation<T>.Create(ref parts, cursor.CreateSpan(chunkStart, parsedChunk.Value >> 1)) : default;
                    return true;
                }

                if (withValue)
                {
                    chunk = cursor.CreateSpan(chunkStart, parsedChunk.Value >> 1);
                }
            }
        }
        finally
        {
            parts.Release();
        }

        value = default;
        return false;
    }

    // A chunk and the delimiter after it, a token when parsing a stream. InterpolationChunk is an internal part of this
    // parser, which isn't entered.
    private bool ReadChunk(ParseContext context, ref ParseResult<int> result)
        => context.IsCompacting ? context.ParseToken(_chunk, ref result) : _chunk.Read(context, ref result);

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_hole is not ISourceable holeSourceable)
        {
            throw new NotSupportedException("Interpolated requires a source-generatable hole parser.");
        }

        // A text literal, or a SkipWhiteSpace of one
        var holeCloseSourceable = (ISourceable)_holeClose;

        var result = context.CreateResult(typeof(Interpolation<T>));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var discard = context.DiscardResult;

        var holeTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var interpolationTypeName = $"global::Parlot.Interpolation<{holeTypeName}>";
        var startName = $"start{context.NextNumber()}";
        var chunkStartName = $"chunkStart{context.NextNumber()}";
        var chunkName = $"chunk{context.NextNumber()}";
        var holesName = $"holes{context.NextNumber()}";

        // The value of the chunk is always used: its length and what ends it
        var chunkHelper = context.WithDiscardResult(false, () => context.Helpers
            .GetOrCreate(_chunk, $"{context.MethodNamePrefix}_Interpolated_Chunk", "int", () => _chunk.GenerateSource(context))
            .MethodName);

        var holeHelper = context.Helpers
            .GetOrCreate(holeSourceable, $"{context.MethodNamePrefix}_Interpolated_Hole", holeTypeName, () => holeSourceable.GenerateSource(context))
            .MethodName;

        var holeCloseHelper = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(holeCloseSourceable, $"{context.MethodNamePrefix}_Interpolated_HoleClose", "string", () => holeCloseSourceable.GenerateSource(context))
            .MethodName);

        result.Body.Add($"var {startName} = {cursorName}.Offset;");

        // The opening text is read by its literal, whose code is span code or a token
        var openHelper = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(_openText, $"{context.MethodNamePrefix}_Interpolated_Open", "string", () => _openText.GenerateSource(context))
            .MethodName);

        result.Body.Add($"if ({openHelper}({ctx}, out _))");
        result.Body.Add("{");

        if (discard)
        {
            // Without the value, the chunks and the holes are read in a loop, and nothing is listed
            result.Body.Add("    while (true)");
            result.Body.Add("    {");
            result.Body.Add($"        if (!{chunkHelper}({ctx}, out var {chunkName}))");
            result.Body.Add("        {");
            result.Body.Add("            break;");
            result.Body.Add("        }");
            result.Body.Add($"        if (({chunkName} & 1) == {InterpolationScanner.Close})");
            result.Body.Add("        {");
            result.Body.Add($"            {result.SuccessVariable} = true;");
            result.Body.Add("            break;");
            result.Body.Add("        }");
            result.Body.Add($"        if (!{holeHelper}({ctx}, out _) || !{holeCloseHelper}({ctx}, out _))");
            result.Body.Add("        {");
            result.Body.Add("            break;");
            result.Body.Add("        }");
            result.Body.Add("    }");
        }
        else
        {
            // A text without a hole, the most common one, is read without a list. The holes are read in a static local
            // function, which alone has the exception handling releasing the list, as ListBuilderSource.EmitFirstItemThenRest.
            result.Body.Add($"    var {chunkStartName} = {cursorName}.Offset;");
            result.Body.Add($"    if ({chunkHelper}({ctx}, out var {chunkName}))");
            result.Body.Add("    {");
            result.Body.Add($"        if (({chunkName} & 1) == {InterpolationScanner.Close})");
            result.Body.Add("        {");
            result.Body.Add($"            {result.ValueVariable} = new {interpolationTypeName}({cursorName}.CreateSpan({chunkStartName}, {chunkName} >> 1));");
            result.Body.Add($"            {result.SuccessVariable} = true;");
            result.Body.Add("        }");
            result.Body.Add("        else");
            result.Body.Add("        {");
            result.Body.Add($"            {result.SuccessVariable} = {holesName}({ctx}, {cursorName}.CreateSpan({chunkStartName}, {chunkName} >> 1), out {result.ValueVariable});");
            result.Body.Add("        }");
            result.Body.Add("    }");

            // A single list, of each hole with the chunk before it: two lists filled in turn would share the stack of the
            // thread when the holes are TextSpans too, which only nested lists can
            var partType = $"(global::Parlot.TextSpan, {holeTypeName})";

            var holes =
                $"    static bool {holesName}(global::Parlot.Fluent.ParseContext {ctx}, global::Parlot.TextSpan chunk, out {interpolationTypeName} value)\n" +
                "    {\n" +
                $"        var {cursorName} = {ctx}.Scanner.Cursor;\n" +
                $"        var parts = new global::Parlot.Fluent.ListBuilder<{partType}>();\n" +
                "        try\n" +
                "        {\n" +
                "            while (true)\n" +
                "            {\n" +
                $"                if (!{holeHelper}({ctx}, out var hole) || !{holeCloseHelper}({ctx}, out _))\n" +
                "                {\n" +
                "                    break;\n" +
                "                }\n" +
                "                parts.Add((chunk, hole));\n" +
                $"                var chunkStart = {cursorName}.Offset;\n" +
                $"                if (!{chunkHelper}({ctx}, out var next))\n" +
                "                {\n" +
                "                    break;\n" +
                "                }\n" +
                $"                chunk = {cursorName}.CreateSpan(chunkStart, next >> 1);\n" +
                $"                if ((next & 1) == {InterpolationScanner.Close})\n" +
                "                {\n" +
                $"                    value = {interpolationTypeName}.Create(ref parts, chunk);\n" +
                "                    return true;\n" +
                "                }\n" +
                "            }\n" +
                "        }\n" +
                "        finally\n" +
                "        {\n" +
                "            parts.Release();\n" +
                "        }\n" +
                "        value = default;\n" +
                "        return false;\n" +
                "    }";

            // A single statement, as in ListBuilderSource.EmitFirstItemThenRest, so that its returns aren't taken for the
            // returns of the parser
            result.Body.Add(holes);
        }

        result.Body.Add($"    if (!{result.SuccessVariable})");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => Name ?? $"Interpolated({_openText.Text}…{_chunk.Close}, {_hole})";
}

/// <summary>
/// A chunk of an interpolated text and the delimiter which ends it, a token of the compacting buffer.
/// </summary>
/// <remarks>
/// The value is the length of the chunk shifted left by one, with <see cref="InterpolationScanner.Hole"/> in the low bit
/// when a hole follows it. The cursor is after the delimiter.
/// </remarks>
internal sealed class InterpolationChunk : Parser<int>, ISourceable, ILoopAnalyzable
{
    private readonly string _holeOpen;
    private readonly char _escape;
    private readonly bool _newLines;
    private readonly string _stopChars;
#if NET8_0_OR_GREATER
    private readonly SearchValues<char> _stops;
#endif

    public InterpolationChunk(string close, string holeOpen, char escape, bool newLines)
    {
        Close = close;
        _holeOpen = holeOpen;
        _escape = escape;
        _newLines = newLines;

        // The chars a chunk can end at, each once
        var stops = close[0].ToString();

        void Add(char c)
        {
            if (stops.IndexOf(c) < 0)
            {
                stops += c;
            }
        }

        Add(holeOpen[0]);

        if (escape != '\0')
        {
            Add(escape);
        }

        if (!newLines)
        {
            Add('\n');
            Add('\r');
        }

        _stopChars = stops;
#if NET8_0_OR_GREATER
        _stops = SearchValues.Create(_stopChars);
#endif
    }

    public string Close { get; }

    public override bool Parse(ParseContext context, ref ParseResult<int> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        // It isn't entered when parsing a string either, where Interpolated reads it directly
        return Read(context, ref result);
    }

    // The delimiter which ends the chunk isn't empty
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => false;

    // Reads the chunk, without entering this parser: Interpolated reads it directly when parsing a string
    public bool Read(ParseContext context, ref ParseResult<int> result)
    {
        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;
#if NET8_0_OR_GREATER
        var length = InterpolationScanner.Scan(cursor.Span, _stops, Close, _holeOpen, _escape, _newLines, out var stop);
#else
        var length = InterpolationScanner.Scan(cursor.Span, _stopChars, Close, _holeOpen, _escape, _newLines, out var stop);
#endif

        // Moving to the end of the text marks that more text could end the chunk, and the failure is recorded where it is
        cursor.AdvanceNoNewLines(length);

        switch (stop)
        {
            case InterpolationScanner.Close:
                cursor.AdvanceNoNewLines(Close.Length);
                break;
            case InterpolationScanner.Hole:
                cursor.AdvanceNoNewLines(_holeOpen.Length);
                break;
            default:
                cursor.ResetPosition(start);
                return false;
        }

        result.Set(start, cursor.Offset, (length << 1) | stop);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(int), () => GenerateSource(context));
        }

        var result = context.CreateResult(typeof(int));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var lengthName = $"length{context.NextNumber()}";
        var stopName = $"stop{context.NextNumber()}";
        var stopsName = $"stops{context.NextNumber()}";

        var stopsField = context.GetSearchValuesField(this, _stopChars);

        var arguments = $"{LiteralHelper.StringToLiteral(Close)}, {LiteralHelper.StringToLiteral(_holeOpen)}, {LiteralHelper.CharToLiteral(_escape)}, {(_newLines ? "true" : "false")}, out var {stopName}";

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        // The stop chars are a SearchValues on .NET 8+ and a string below. Only the code outside of the directives names
        // Parlot's types, which the standalone generation moves to the embedded runtime: it doesn't see the disabled code.
        result.Body.Add("#if NET8_0_OR_GREATER");
        result.Body.Add($"var {stopsName} = {stopsField};");
        result.Body.Add("#else");
        result.Body.Add($"var {stopsName} = {LiteralHelper.StringToLiteral(_stopChars)};");
        result.Body.Add("#endif");
        result.Body.Add($"var {lengthName} = global::Parlot.InterpolationScanner.Scan({cursorName}.Span, {stopsName}, {arguments});");
        result.Body.Add($"{cursorName}.AdvanceNoNewLines({lengthName});");
        result.Body.Add($"if ({stopName} == {InterpolationScanner.Close})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.AdvanceNoNewLines({Close.Length});");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        result.Body.Add($"else if ({stopName} == {InterpolationScanner.Hole})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.AdvanceNoNewLines({_holeOpen.Length});");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");
        result.Body.Add($"{result.ValueVariable} = ({lengthName} << 1) | {stopName};");

        return result;
    }

    public override string ToString() => "InterpolationChunk";
}
