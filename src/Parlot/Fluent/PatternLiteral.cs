using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class PatternLiteral : Parser<TextSpan>, ISourceable, ILoopAnalyzable
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

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => _minSize <= 0;

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
            if (_minSize > 0)
            {
                context.ExitParser(this);
                return false;
            }

            // A pattern without a minimum size matches nothing, like the generated parser and AnyOf do.
            // Reaching the end of a non-final buffer has already marked the cursor.
            var offset = cursor.Offset;
            result.Set(offset, offset, cursor.CreateSpan(offset, 0));

            context.ExitParser(this);
            return true;
        }

        // The chars are counted first such that the cursor only moves once, and not at all when the size constraint is not met.

        var span = cursor.Span;
        var limit = _maxSize > 0 && _maxSize < span.Length ? _maxSize : span.Length;
        var size = 1;
        var newLines = span[0] == '\n';

        while (size < limit)
        {
            var c = span[size];

            if (!predicate(c))
            {
                break;
            }

            newLines |= c == '\n';
            size++;
        }

        if (size >= _minSize)
        {
            var start = cursor.Offset;

            // The line and column only need to be tracked char by char when there are new lines
            if (newLines)
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

        var spanName = $"span{context.NextNumber()}";
        var sizeName = $"size{context.NextNumber()}";
        var newLinesName = $"newLines{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";
        var cName = $"c{context.NextNumber()}";

        var predicateLambda = context.RegisterLambda(_predicate);

        // The same shape as Parse: the chars are counted on the span first such that the cursor only moves once,
        // and not at all when the size constraint is not met.
        if (_minSize <= 0)
        {
            // A pattern without a minimum size matches nothing
            result.Body.Add($"if ({cursorName}.Eof || !{predicateLambda}({cursorName}.Current))");
            result.Body.Add("{");
            if (!context.DiscardResult)
            {
                result.Body.Add($"    {result.ValueVariable} = {cursorName}.CreateSpan({cursorName}.Offset, 0);");
            }
            result.Body.Add($"    {result.SuccessVariable} = true;");
            result.Body.Add("}");
            result.Body.Add("else");
        }
        else
        {
            result.Body.Add($"if (!{cursorName}.Eof && {predicateLambda}({cursorName}.Current))");
        }

        result.Body.Add("{");
        result.Body.Add($"    var {spanName} = {cursorName}.Span;");

        var limitName = $"{spanName}.Length";

        if (_maxSize > 0)
        {
            limitName = $"limit{context.NextNumber()}";
            result.Body.Add($"    var {limitName} = {_maxSize} < {spanName}.Length ? {_maxSize} : {spanName}.Length;");
        }

        result.Body.Add($"    var {sizeName} = 1;");
        result.Body.Add($"    var {newLinesName} = {spanName}[0] == '\\n';");
        result.Body.Add($"    while ({sizeName} < {limitName})");
        result.Body.Add("    {");
        result.Body.Add($"        var {cName} = {spanName}[{sizeName}];");
        result.Body.Add($"        if (!{predicateLambda}({cName}))");
        result.Body.Add("        {");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add($"        {newLinesName} |= {cName} == '\\n';");
        result.Body.Add($"        {sizeName}++;");
        result.Body.Add("    }");

        var indent = "    ";

        if (_minSize > 1)
        {
            result.Body.Add($"    if ({sizeName} >= {_minSize})");
            result.Body.Add("    {");
            indent = "        ";
        }

        result.Body.Add($"{indent}var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{indent}if ({newLinesName})");
        result.Body.Add($"{indent}{{");
        result.Body.Add($"{indent}    {cursorName}.Advance({sizeName});");
        result.Body.Add($"{indent}}}");
        result.Body.Add($"{indent}else");
        result.Body.Add($"{indent}{{");
        result.Body.Add($"{indent}    {cursorName}.AdvanceBy({sizeName}, 0, {sizeName});");
        result.Body.Add($"{indent}}}");
        if (!context.DiscardResult)
        {
            result.Body.Add($"{indent}{result.ValueVariable} = {cursorName}.CreateSpan({startName}, {sizeName});");
        }
        result.Body.Add($"{indent}{result.SuccessVariable} = true;");

        if (_minSize > 1)
        {
            result.Body.Add("    }");
            result.Body.Add($"    else if ({sizeName} == {spanName}.Length)");
            result.Body.Add("    {");
            result.Body.Add("        // More matching chars could follow the end of the buffer");
            result.Body.Add($"        {cursorName}.MarkHitEnd();");
            result.Body.Add("    }");
        }

        result.Body.Add("}");

        return result;
    }
}
