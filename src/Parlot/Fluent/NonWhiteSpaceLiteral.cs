using Parlot.SourceGeneration;

namespace Parlot.Fluent;

public sealed class NonWhiteSpaceLiteral : Parser<TextSpan>, ISourceable, ILoopAnalyzable
{
    private readonly bool _includeNewLines;

    public NonWhiteSpaceLiteral(bool includeNewLines = true)
    {
        _includeNewLines = includeNewLines;
        Name = "NonWhiteSpaceLiteral";
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => false;

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        if (context.Scanner.Cursor.Eof)
        {
            context.ExitParser(this);
            return false;
        }

        var start = context.Scanner.Cursor.Offset;

        context.Scanner.ReadNonWhiteSpace(_includeNewLines ? context.WhiteSpaceOrNewLineMask : context.WhiteSpaceMask, out _);

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
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;

        var startName = $"start{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";

        result.Body.Add($"if (!{cursorName}.Eof)");
        result.Body.Add("{");
        result.Body.Add($"    var {startName} = {cursorName}.Offset;");

        var maskName = _includeNewLines ? "WhiteSpaceOrNewLineMask" : "WhiteSpaceMask";
        result.Body.Add($"    {scannerName}.ReadNonWhiteSpace({context.ParseContextName}.{maskName}, out _);");

        result.Body.Add($"    var {endName} = {cursorName}.Offset;");
        result.Body.Add($"    if ({startName} != {endName})");
        result.Body.Add("    {");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {result.ValueVariable} = {context.CursorName}.CreateSpan({startName}, {endName} - {startName});");
        }
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }
}
