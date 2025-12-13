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

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;

        if (Parser is not Parlot.SourceGeneration.ISourceable sourceable)
        {
            result.Body.Add($"{result.SuccessVariable} = false;");
            return result;
        }

        // Register helper for the inner parser
        static Type GetParserValueType(object parser)
        {
            var type = parser.GetType();
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "Parlot.Fluent.Parser`1")
                {
                    return type.GetGenericArguments()[0];
                }
                type = type.BaseType!;
            }
            throw new InvalidOperationException("Unable to determine parser value type.");
        }

        var valueTypeName = SourceGenerationContext.GetTypeName(GetParserValueType(sourceable));
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_SkipWS", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var shortcutName = $"shortcut{context.NextNumber()}";
        var contentStartOffsetName = $"contentStartOffset{context.NextNumber()}";

        result.Body.Add($"var {startName} = default(global::Parlot.TextPosition);");
        result.Body.Add($"var {shortcutName} = {ctx}.WhiteSpaceParser is null && !global::Parlot.Character.IsWhiteSpaceOrNewLine({cursorName}.Current);");
        result.Body.Add($"var {contentStartOffsetName} = {cursorName}.Offset;");

        result.Body.Add($"if (!{shortcutName})");
        result.Body.Add("{");
        result.Body.Add($"    {startName} = {cursorName}.Position;");
        result.Body.Add($"    {ctx}.SkipWhiteSpace();");
        result.Body.Add($"    {contentStartOffsetName} = {cursorName}.Offset;");
        result.Body.Add("}");

        // Track the content start offset variable so wrappers can use it for span tracking
        result.ContentStartOffsetVariable = contentStartOffsetName;

        // Call the inner parser using helper
        result.Body.Add($"{result.SuccessVariable} = {helperName}({ctx}, out {result.ValueVariable});");
        result.Body.Add($"if (!{result.SuccessVariable} && !{shortcutName})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

public override string ToString() => $"{Parser} (Skip WS)";
}
