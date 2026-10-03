using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// A parser that succeeds when parsing whitespaces as defined in <see cref="ParseContext.WhiteSpaceParser"/>.
/// </summary>
public sealed class WhiteSpaceParser : Parser<TextSpan>, ISourceable
{
    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var start = context.Scanner.Cursor.Offset;

        context.SkipWhiteSpace();

        var end = context.Scanner.Cursor.Offset;

        if (start == end)
        {
            context.ExitParser(this);
            return false;
        }

        result.Set(start, end, context.Scanner.Cursor.CreateSpan(start, end - start));

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
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;

        var startName = $"start{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{ctx}.SkipWhiteSpace();");
        result.Body.Add($"var {endName} = {cursorName}.Offset;");
        
        result.Body.Add($"if ({startName} != {endName})");
        result.Body.Add("{");
        result.Body.Add($"    {result.ValueVariable} = {context.CursorName}.CreateSpan({startName}, {endName} - {startName});");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => "WhiteSpaceParser";
}
