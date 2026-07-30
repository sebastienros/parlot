using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class NonWhiteSpaceLiteral : Parser<TextSpan>, ISourceable
{
    private readonly bool _includeNewLines;

    public NonWhiteSpaceLiteral(bool includeNewLines = true)
    {
        _includeNewLines = includeNewLines;
        Name = "NonWhiteSpaceLiteral";
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        if (context.Scanner.Cursor.Eof)
        {
            context.ExitParser(this);
            return false;
        }

        var start = context.Scanner.Cursor.Offset;

        if (_includeNewLines)
        {
            context.Scanner.ReadNonWhiteSpaceOrNewLine();
        }
        else
        {
            context.Scanner.ReadNonWhiteSpace();
        }

        var end = context.Scanner.Cursor.Offset;

        if (start == end)
        {
            context.ExitParser(this);
            return false;
        }

        result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));

        context.ExitParser(this);
        return true;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(TextSpan));
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;

        var startName = $"start{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";

        result.Body.Add($"if (!{cursorName}.Eof)");
        result.Body.Add("{");
        result.Body.Add($"    var {startName} = {cursorName}.Offset;");
        
        if (_includeNewLines)
        {
            result.Body.Add($"    {scannerName}.ReadNonWhiteSpaceOrNewLine();");
        }
        else
        {
            result.Body.Add($"    {scannerName}.ReadNonWhiteSpace();");
        }

        result.Body.Add($"    var {endName} = {cursorName}.Offset;");
        result.Body.Add($"    if ({startName} != {endName})");
        result.Body.Add("    {");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {result.ValueVariable} = new global::Parlot.TextSpan({scannerName}.Buffer, {startName}, {endName} - {startName});");
        }
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }
}
