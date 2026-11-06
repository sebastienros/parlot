using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public enum StringLiteralQuotes
{
    Single,
    Double,
    Backtick,
    SingleOrDouble,
    Custom
}

public sealed class StringLiteral : Parser<TextSpan>, ISeekable, ISourceable, ILoopAnalyzable
{
    static readonly char[] SingleQuotes = ['\''];
    static readonly char[] DoubleQuotes = ['\"'];
    static readonly char[] Backtick = ['`'];
    static readonly char[] SingleOrDoubleQuotes = ['\'', '\"'];

    private readonly StringLiteralQuotes _quotes;

    // The escape sequences the literal accepts, and how it decodes them
    private readonly StringEscapes _escapes;

    // The pool the decoded strings come from, for a literal read by a pooled string
    private readonly StringPool? _pool;

    public StringLiteral(StringLiteralQuotes quotes) : this(quotes, StringEscapes.Default)
    {
    }

    /// <summary>
    /// A string literal enclosed in <paramref name="quotes"/>, with the escape sequences of <paramref name="escapes"/>.
    /// </summary>
    public StringLiteral(StringLiteralQuotes quotes, StringEscapes escapes)
    {
        _quotes = quotes;
        _escapes = escapes;

        ExpectedChars = _quotes switch
        {
            StringLiteralQuotes.Single => SingleQuotes,
            StringLiteralQuotes.Double => DoubleQuotes,
            StringLiteralQuotes.Backtick => Backtick,
            StringLiteralQuotes.SingleOrDouble => SingleOrDoubleQuotes,
            _ => throw new InvalidOperationException()
        };

        Name = "StringLiteral";
    }

    public StringLiteral(char quote) : this(quote, StringEscapes.Default)
    {
    }

    /// <summary>
    /// A string literal enclosed in <paramref name="quote"/>, with the escape sequences of <paramref name="escapes"/>.
    /// </summary>
    public StringLiteral(char quote, StringEscapes escapes)
    {
        _escapes = escapes;
        _quotes = quote switch
        {
            '\'' => StringLiteralQuotes.Single,
            '\"' => StringLiteralQuotes.Double,
            '`' => StringLiteralQuotes.Backtick,
            _ => StringLiteralQuotes.Custom,
        };

        ExpectedChars = [quote];

        Name = "StringLiteral";
    }

    /// <summary>
    /// A copy of <paramref name="source"/> which takes the strings it decodes from <paramref name="pool"/>.
    /// </summary>
    internal StringLiteral(StringLiteral source, StringPool pool)
    {
        _quotes = source._quotes;
        _escapes = source._escapes;
        _pool = pool;
        ExpectedChars = source.ExpectedChars;
        Name = source.Name;
    }

    public bool CanSeek { get; } = true;

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    /// <summary>
    /// The escape sequences the literal accepts, and how it decodes them.
    /// </summary>
    public StringEscapes Escapes => _escapes;

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => false;

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        // Any of the quotes can start the string, the other kinds have a single one
        var quote = ExpectedChars[0];

        if (_quotes == StringLiteralQuotes.SingleOrDouble && cursor.Current == '"')
        {
            quote = '"';
        }

        // The default escapes keep their own scanner and decoder
        ReadOnlySpan<char> text;
        bool hasEscape;
        var read = _escapes == StringEscapes.Default
            ? context.Scanner.ReadQuotedString(quote, out text, out hasEscape)
            : context.Scanner.ReadQuotedString(quote, _escapes, out text, out hasEscape);

        if (!read)
        {
            context.ExitParser(this);
            return false;
        }

        var end = cursor.Offset;

        // The scanner knows whether the string has escape sequences, so a string without any isn't read again
        if (hasEscape)
        {
            var content = text.Slice(1, text.Length - 2);
            var decoded = _escapes == StringEscapes.Default
                ? Character.DecodeStringInternal(content, _pool)
                : Character.DecodeStringInternal(content, _escapes, quote, _pool);

            result.Set(start, end, new TextSpan(decoded));
        }
        else
        {
            // Remove quotes
            result.Set(start, end, new TextSpan(context.Scanner.Buffer, start + 1 - cursor.BufferStart, end - start - 2));
        }

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(TextSpan), () => GenerateSource(context));
        }

        var result = context.CreateResult(typeof(TextSpan));
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;

        var startName = $"start{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";
        var textName = $"text{context.NextNumber()}";
        var hasEscapeName = $"hasEscape{context.NextNumber()}";

        if (!context.DiscardResult)
        {
            result.Body.Add($"var {startName} = {cursorName}.Offset;");
        }

        // Any of the quotes can start the string, the other kinds have a single one
        var quote = _quotes == StringLiteralQuotes.SingleOrDouble
            ? $"({cursorName}.Current == (char)34 ? (char)34 : (char)39)"
            : $"(char){(int)ExpectedChars[0]}";

        // The default escapes keep the overload without them
        var escapesArgument = $"(global::Parlot.Fluent.StringEscapes){(int)_escapes}";
        var escapes = _escapes == StringEscapes.Default ? "" : escapesArgument + ", ";

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({scannerName}.ReadQuotedString({quote}, {escapes}out _, out _))");
        }
        else
        {
            result.Body.Add($"if ({scannerName}.ReadQuotedString({quote}, {escapes}out var {textName}, out var {hasEscapeName}))");
        }

        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    var {endName} = {cursorName}.Offset;");
            // The scanner knows whether the string has escape sequences, so a string without any isn't read again
            result.Body.Add($"    if ({hasEscapeName})");
            result.Body.Add("    {");
            // The other escapes also need the quote, the first char of the text, and take the pool even when there is none
            var pool = _pool != null ? ", " + context.GetStringPoolField(_pool) : "";
            var decodeArguments = _escapes == StringEscapes.Default ? pool : $", {escapesArgument}, {textName}[0]" + (_pool != null ? pool : ", null");
            result.Body.Add($"        {result.ValueVariable} = new global::Parlot.TextSpan(global::Parlot.Character.DecodeStringInternal({textName}.Slice(1, {textName}.Length - 2){decodeArguments}));");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            // Buffer windows are immutable strings, so the span can reference the current one. BufferStart is a
            // constant 0 in assemblies without TextReader entry points.
            result.Body.Add($"        {result.ValueVariable} = new global::Parlot.TextSpan({scannerName}.Buffer, {startName} + 1 - {cursorName}.BufferStart, {endName} - {startName} - 2);");
            result.Body.Add("    }");
        }
        result.Body.Add("}");

        return result;
    }
}
