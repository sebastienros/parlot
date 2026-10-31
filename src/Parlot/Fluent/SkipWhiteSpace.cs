using Parlot.Rewriting;
using System;
using Parlot.SourceGeneration;

namespace Parlot.Fluent;

public sealed class SkipWhiteSpace<T> : Parser<T>, ISeekablePrefixes, ISourceable
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

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(Parser);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        // Shortcut for common scenario
        if (!context.MaySkipWhiteSpace(cursor.Current))
        {
            context.ExitParser(this);
            return Parser.Parse(context, ref result);
        }

        var start = cursor.Offset;

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

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;

        if (Parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("SkipWhiteSpace requires a source-generatable parser.");
        }

        // Register helper for the inner parser
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_SkipWS", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        var cursorName = context.CursorName;
        var shortcutName = $"shortcut{context.NextNumber()}";
        var contentStartOffsetName = $"contentStart{context.NextNumber()}";

        // The check of ParseContext.MaySkipWhiteSpace. Every set is part of the default one, whose constant mask tells most
        // chars apart in generated code, which measured faster there than reading the set's bit first
        result.Body.Add($"var {shortcutName} = {ctx}.WhiteSpaceParser is null && (!global::Parlot.Character.IsWhiteSpaceOrNewLine({cursorName}.Current) || !global::Parlot.Character.HasMask({cursorName}.Current, {ctx}._skipMask));");
        result.Body.Add($"var {contentStartOffsetName} = {cursorName}.Offset;");

        result.Body.Add($"if (!{shortcutName})");
        result.Body.Add("{");
        result.Body.Add($"    var start = {cursorName}.Offset;");
        result.Body.Add($"    {ctx}.SkipWhiteSpace();");
        result.Body.Add($"    {contentStartOffsetName} = {cursorName}.Offset;");
        if (context.DiscardResult)
        {
            result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out _);");
        }
        else
        {
            result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out {result.ValueVariable});");
        }
        result.Body.Add($"    if (!{result.SuccessVariable})");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition(start);");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        if (context.DiscardResult)
        {
            result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out _);");
        }
        else
        {
            result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out {result.ValueVariable});");
        }
        result.Body.Add("}");

        // Track the content start offset for Core method to use
        result.ContentStartOffsetVariable = contentStartOffsetName;

        return result;
    }

public override string ToString() => $"{Parser} (Skip WS)";
}
