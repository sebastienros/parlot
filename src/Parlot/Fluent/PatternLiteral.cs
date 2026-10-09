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
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var predicate = _predicate;

        if (cursor.Eof || !predicate(cursor.Current))
        {
            context.ExitParser(this);
            return false;
        }

        // The chars are counted first such that the cursor only moves once, and not at all when the size constraint is not met.

        var span = cursor.Span;
        var limit = _maxSize > 0 && _maxSize < span.Length ? _maxSize : span.Length;
        var size = 1;
        var newLines = span[0] is '\n' or '\r';

        while (size < limit)
        {
            var c = span[size];

            if (!predicate(c))
            {
                break;
            }

            newLines |= c is '\n' or '\r';
            size++;
        }

        if (size >= _minSize)
        {
            var start = cursor.Offset;

            // The line and column only need to be tracked char by char when there are new lines,
            // including the char the cursor moves to since a '\r' doesn't count as a column.
            if (newLines || (size < span.Length && span[size] is '\n' or '\r'))
            {
                cursor.Advance(size);
            }
            else
            {
                cursor.AdvanceBy(size, 0, size);
            }

            result.Set(start, start + size, cursor.CreateSpan(start, size));

            context.ExitParser(this);
            return true;
        }

        if (size == span.Length)
        {
            // More matching chars could follow the end of the buffer
            cursor.MarkHitEnd();
        }

        context.ExitParser(this);
        return false;
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
            result.Body.Add($"    {result.ValueVariable} = {context.CursorName}.CreateSpan({startName}.Offset, end{context.NextNumber() - 1} - {startName}.Offset);");
        }
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }
}
