using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class PatternLiteral : Parser<TextSpan>, ISourceable
{
    private readonly Func<char, bool> _predicate;
    private readonly int _minSize;
    private readonly int _maxSize;

    public PatternLiteral(Func<char, bool> predicate, int minSize = 1, int maxSize = 0)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _minSize = minSize;
        _maxSize = maxSize;

        Name = "PatternLiteral";
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        if (context.Scanner.Cursor.Eof || !_predicate(context.Scanner.Cursor.Current))
        {
            context.ExitParser(this);
            return false;
        }

        var startPosition = context.Scanner.Cursor.Position;
        var start = startPosition.Offset;

        context.Scanner.Cursor.Advance();
        var size = 1;

        while (!context.Scanner.Cursor.Eof && (_maxSize <= 0 || size < _maxSize) && _predicate(context.Scanner.Cursor.Current))
        {
            context.Scanner.Cursor.Advance();
            size++;
        }

        if (size >= _minSize)
        {
            var end = context.Scanner.Cursor.Offset;
            result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));

            context.ExitParser(this);
            return true;
        }

        // When the size constraint has not been met the parser may still have advanced the cursor.
        context.Scanner.Cursor.ResetPosition(startPosition);

        context.ExitParser(this);
        return false;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(TextSpan));
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;

        var startName = $"start{context.NextNumber()}";
        var sizeName = $"size{context.NextNumber()}";

        result.Body.Add($"var {startName} = {cursorName}.Position;");
        result.Body.Add($"var {sizeName} = 0;");

        // Register the predicate lambda
        var predicateLambda = context.RegisterLambda(_predicate);

        result.Body.Add("while (true)");
        result.Body.Add("{");
        result.Body.Add($"    if ({cursorName}.Eof) break;");
        result.Body.Add($"    if (!{predicateLambda}({cursorName}.Current)) break;");
        result.Body.Add($"    {cursorName}.Advance();");
        result.Body.Add($"    {sizeName}++;");
        if (_maxSize > 0)
        {
            result.Body.Add($"    if ({sizeName} == {_maxSize}) break;");
        }
        result.Body.Add("}");

        result.Body.Add($"if ({sizeName} < {_minSize})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    var end{context.NextNumber()} = {cursorName}.Offset;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = new global::Parlot.TextSpan({scannerName}.Buffer, {startName}.Offset, end{context.NextNumber() - 1} - {startName}.Offset);");
        }
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }
}
