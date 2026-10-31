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

    public StringLiteral(StringLiteralQuotes quotes)
    {
        _quotes = quotes;

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

    public StringLiteral(char quote)
    {
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

    public bool CanSeek { get; } = true;

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

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

        if (!context.Scanner.ReadQuotedString(quote, out var text, out var hasEscape))
        {
            context.ExitParser(this);
            return false;
        }

        var end = cursor.Offset;

        // The scanner knows whether the string has escape sequences, so a string without any isn't read again
        if (hasEscape)
        {
            result.Set(start, end, new TextSpan(Character.DecodeStringInternal(text.Slice(1, text.Length - 2))));
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

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({scannerName}.ReadQuotedString({quote}, out _, out _))");
        }
        else
        {
            result.Body.Add($"if ({scannerName}.ReadQuotedString({quote}, out var {textName}, out var {hasEscapeName}))");
        }

        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    var {endName} = {cursorName}.Offset;");
            // The scanner knows whether the string has escape sequences, so a string without any isn't read again
            result.Body.Add($"    if ({hasEscapeName})");
            result.Body.Add("    {");
            result.Body.Add($"        {result.ValueVariable} = new global::Parlot.TextSpan(global::Parlot.Character.DecodeStringInternal({textName}.Slice(1, {textName}.Length - 2)));");
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
