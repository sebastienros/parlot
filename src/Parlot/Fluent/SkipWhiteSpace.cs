using Parlot;
using Parlot.Rewriting;
using System;
using Parlot.SourceGeneration;

namespace Parlot.Fluent;

public sealed class SkipWhiteSpace<T> : Parser<T>, ISeekable, ISourceable
{
    public Parser<T> Parser { get; }

    public SkipWhiteSpace(Parser<T> parser)
    {
        Parser = parser ?? throw new ArgumentNullException(nameof(parser));

        if (parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; } = true;

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        // Shortcut for common scenario
        if (context.WhiteSpaceParser is null && !Character.IsWhiteSpaceOrNewLine(cursor.Current))
        {
            context.ExitParser(this);
            return Parser.Parse(context, ref result);
        }

        var start = cursor.Position;

        // Use the scanner's logic to ignore whitespaces since it knows about multi-line grammars
        context.SkipWhiteSpace();

        if (Parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            return true;
        }

        cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    
    public Parlot.SourceGeneration.SourceResult GenerateSource(Parlot.SourceGeneration.SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // This initial implementation delegates to the existing Parse method, so that
        // callers can already exercise the source-generation pipeline without having
        // to special-case this combinator.
        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;

        if (Parser is not Parlot.SourceGeneration.ISourceable sourceable)
        {
            result.Body.Add($"{result.SuccessVariable} = false;");
            return result;
        }

        var inner = sourceable.GenerateSource(context);

        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";

        result.Body.Add($"var {startName} = default(global::Parlot.TextPosition);");

        result.Body.Add($"{startName} = {cursorName}.Position;");

        foreach (var local in inner.Locals)
        {
            result.Body.Add(local);
        }

        result.Body.Add($"{ctx}.SkipWhiteSpace();");

        foreach (var stmt in inner.Body)
        {
            result.Body.Add(stmt);
        }

        result.Body.Add($"if ({inner.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add($"    {result.ValueVariable} = {inner.ValueVariable};");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add($"    {result.SuccessVariable} = false;");
        result.Body.Add("}");

        return result;
    }

public override string ToString() => $"{Parser} (Skip WS)";
}
