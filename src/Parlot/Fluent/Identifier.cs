using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class Identifier : Parser<TextSpan>, ISeekable, ISourceable, ILoopAnalyzable
{
    private readonly Func<char, bool>? _extraStart;
    private readonly Func<char, bool>? _extraPart;

    public Identifier(Func<char, bool>? extraStart = null, Func<char, bool>? extraPart = null)
    {
        _extraStart = extraStart;
        _extraPart = extraPart;

        // The chars an extra start predicate accepts are unknown
        if (extraStart == null)
        {
            CanSeek = true;
            ExpectedChars = Character._identifierStartChars;
        }

        Name = "Identifier";
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    // A custom start can accept the end of the text, where the cursor doesn't move
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => _extraStart != null;

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var first = cursor.Current;

        if (Character.IsIdentifierStart(first) || (_extraStart != null && _extraStart(first)))
        {
            var start = cursor.Offset;

            // At this point we have an identifier, read while it's an identifier part.
            // The chars are counted first such that the cursor only moves once.

            var span = cursor.Span;

            if (span.IsEmpty)
            {
                // A custom start predicate accepted the end of the text
                cursor.AdvanceNoNewLines(1);
            }
            else
            {
                var extraPart = _extraPart;
                var size = 1;

                while (size < span.Length)
                {
                    var c = span[size];

                    if (!Character.IsIdentifierPart(c) && (extraPart == null || !extraPart(c)))
                    {
                        break;
                    }

                    size++;
                }

                cursor.AdvanceNoNewLines(size);
            }

            var end = cursor.Offset;

            result.Set(start, end, cursor.CreateSpan(start, end - start));

            context.ExitParser(this);
            return true;
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

        var firstCharName = $"first{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";
        var spanName = $"span{context.NextNumber()}";
        var sizeName = $"size{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";

        result.Body.Add($"var {firstCharName} = {cursorName}.Current;");

        var startCondition = $"global::Parlot.Character.IsIdentifierStart({firstCharName})";

        if (_extraStart != null)
        {
            var extraStartLambda = context.RegisterLambda(_extraStart);
            startCondition = $"{startCondition} || {extraStartLambda}({firstCharName})";
        }

        result.Body.Add($"if ({startCondition})");
        result.Body.Add("{");
        result.Body.Add($"    var {startName} = {cursorName}.Offset;");
        result.Body.Add($"    var {spanName} = {cursorName}.Span;");

        // The chars are counted on the span first such that the cursor only moves once, like the runtime parser.
        // Only an extra start predicate can accept the end of the text, where the span is empty.
        var indent = "    ";

        if (_extraStart != null)
        {
            result.Body.Add($"    if ({spanName}.IsEmpty)");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.AdvanceNoNewLines(1);");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            indent = "        ";
        }

        if (_extraPart == null)
        {
            // The default identifier part chars are searched with SearchValues on .NET 8+, like IdentifierLiteral
            var indexName = $"index{context.NextNumber()}";

            result.Body.Add("#if NET8_0_OR_GREATER");
            result.Body.Add($"{indent}var {indexName} = {spanName}.Slice(1).IndexOfAnyExcept(global::Parlot.Character._identifierPart);");
            result.Body.Add($"{indent}var {sizeName} = {indexName} == -1 ? {spanName}.Length : {indexName} + 1;");
            result.Body.Add("#else");
            GenerateCountLoop(context, result, indent, spanName, sizeName, null);
            result.Body.Add("#endif");
        }
        else
        {
            var extraPartLambda = context.RegisterLambda(_extraPart);
            GenerateCountLoop(context, result, indent, spanName, sizeName, extraPartLambda);
        }

        result.Body.Add($"{indent}{cursorName}.AdvanceNoNewLines({sizeName});");

        if (_extraStart != null)
        {
            result.Body.Add("    }");
        }

        result.Body.Add($"    var {endName} = {cursorName}.Offset;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = {cursorName}.CreateSpan({startName}, {endName} - {startName});");
        }
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    // Counts the identifier part chars following the first one
    private static void GenerateCountLoop(SourceGenerationContext context, SourceResult result, string indent, string spanName, string sizeName, string? extraPartLambda)
    {
        var cName = $"c{context.NextNumber()}";
        var partCondition = $"!global::Parlot.Character.IsIdentifierPart({cName})";

        if (extraPartLambda != null)
        {
            partCondition = $"{partCondition} && !{extraPartLambda}({cName})";
        }

        result.Body.Add($"{indent}var {sizeName} = 1;");
        result.Body.Add($"{indent}while ({sizeName} < {spanName}.Length)");
        result.Body.Add($"{indent}{{");
        result.Body.Add($"{indent}    var {cName} = {spanName}[{sizeName}];");
        result.Body.Add($"{indent}    if ({partCondition})");
        result.Body.Add($"{indent}    {{");
        result.Body.Add($"{indent}        break;");
        result.Body.Add($"{indent}    }}");
        result.Body.Add($"{indent}    {sizeName}++;");
        result.Body.Add($"{indent}}}");
    }
}
