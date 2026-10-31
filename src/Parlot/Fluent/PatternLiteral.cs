using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class PatternLiteral : Parser<TextSpan>, ISourceable, ILoopAnalyzable, ISpanSourceable
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

        while (size < limit && predicate(span[size]))
        {
            size++;
        }

        if (size >= _minSize)
        {
            var start = cursor.Offset;

            cursor.AdvanceNoNewLines(size);

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
        result.Body.Add($"    while ({sizeName} < {limitName})");
        result.Body.Add("    {");
        result.Body.Add($"        var {cName} = {spanName}[{sizeName}];");
        result.Body.Add($"        if (!{predicateLambda}({cName}))");
        result.Body.Add("        {");
        result.Body.Add("            break;");
        result.Body.Add("        }");
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
        result.Body.Add($"{indent}{cursorName}.AdvanceNoNewLines({sizeName});");
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

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => 1;

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var span = writer.Span;
        var pos = writer.Pos;
        var predicateLambda = writer.Context.RegisterLambda(_predicate);
        var start = writer.DeclareLocal("int", "patternStart", "0");

        writer.Add($"{start} = {pos};");

        // The same shape as Parse: the first char, then the following ones up to the maximum size
        var firstChar = $"(uint){pos} < (uint){span}.Length && {predicateLambda}({span}[{pos}])";

        if (_minSize <= 0)
        {
            // A pattern without a minimum size matches nothing
            writer.BeginBlock($"if ({firstChar})");
        }
        else
        {
            writer.BeginBlock($"if (!({firstChar}))");
            writer.Fail(fail);
            writer.EndBlock();
        }

        writer.Add($"{pos}++;");

        if (_maxSize != 1)
        {
            var limit = $"{span}.Length";

            if (_maxSize > 0)
            {
                limit = writer.DeclareLocal("int", "patternLimit", "0");
                writer.Add($"{limit} = {span}.Length - {start} > {_maxSize} ? {start} + {_maxSize} : {span}.Length;");
            }

            writer.Add($"while ({pos} < {limit} && {predicateLambda}({span}[{pos}])) {pos}++;");
        }

        if (_minSize <= 0)
        {
            writer.EndBlock();
            writer.Add($"else if ({writer.AtEnd}) {writer.MarkHitEnd}");
        }
        else if (_minSize > 1)
        {
            writer.BeginBlock($"if ({pos} - {start} < {_minSize})");
            // More matching chars could follow the end of the span
            writer.Fail(fail);
            writer.EndBlock();
        }

        if (valueVariable != null)
        {
            writer.Add($"{valueVariable} = {writer.CreateSpan(start, $"{pos} - {start}")};");
        }
    }
}
