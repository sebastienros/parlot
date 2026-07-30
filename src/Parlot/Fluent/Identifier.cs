using Parlot.SourceGeneration;
using System;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class Identifier : Parser<TextSpan>, ISourceable
{
    private static readonly MethodInfo _isIdentifierStartMethodInfo = typeof(Character).GetMethod(nameof(Character.IsIdentifierStart))!;
    private static readonly MethodInfo _isIdentifierPartMethodInfo = typeof(Character).GetMethod(nameof(Character.IsIdentifierPart))!;

    private readonly Func<char, bool>? _extraStart;
    private readonly Func<char, bool>? _extraPart;

    public Identifier(Func<char, bool>? extraStart = null, Func<char, bool>? extraPart = null)
    {
        _extraStart = extraStart;
        _extraPart = extraPart;

        Name = "Identifier";
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var first = context.Scanner.Cursor.Current;

        if (Character.IsIdentifierStart(first) || (_extraStart != null && _extraStart(first)))
        {
            var start = context.Scanner.Cursor.Offset;

            // At this point we have an identifier, read while it's an identifier part.

            context.Scanner.Cursor.AdvanceNoNewLines(1);

            while (!context.Scanner.Cursor.Eof && (Character.IsIdentifierPart(context.Scanner.Cursor.Current) || (_extraPart != null && _extraPart(context.Scanner.Cursor.Current))))
            {
                context.Scanner.Cursor.AdvanceNoNewLines(1);
            }

            var end = context.Scanner.Cursor.Offset;

            result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(TextSpan));
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;

        var firstCharName = $"first{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";

        result.Body.Add($"var {firstCharName} = {cursorName}.Current;");

        // Check if first char is an identifier start
        var startCondition = _extraStart != null
            ? $"global::Parlot.Character.IsIdentifierStart({firstCharName}) || extraStart{context.NextNumber()}({firstCharName})"
            : $"global::Parlot.Character.IsIdentifierStart({firstCharName})";

        if (_extraStart != null)
        {
            var extraStartLambda = context.RegisterLambda(_extraStart);
            startCondition = $"global::Parlot.Character.IsIdentifierStart({firstCharName}) || {extraStartLambda}({firstCharName})";
        }

        result.Body.Add($"if ({startCondition})");
        result.Body.Add("{");
        result.Body.Add($"    var {startName} = {cursorName}.Offset;");
        result.Body.Add($"    {cursorName}.AdvanceNoNewLines(1);");

        // Continue reading while it's an identifier part
        var partCondition = _extraPart != null
            ? $"!{cursorName}.Eof && (global::Parlot.Character.IsIdentifierPart({cursorName}.Current) || extraPart{context.NextNumber()}({cursorName}.Current))"
            : $"!{cursorName}.Eof && global::Parlot.Character.IsIdentifierPart({cursorName}.Current)";

        if (_extraPart != null)
        {
            var extraPartLambda = context.RegisterLambda(_extraPart);
            partCondition = $"!{cursorName}.Eof && (global::Parlot.Character.IsIdentifierPart({cursorName}.Current) || {extraPartLambda}({cursorName}.Current))";
        }

        result.Body.Add($"    while ({partCondition})");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.AdvanceNoNewLines(1);");
        result.Body.Add("    }");

        result.Body.Add($"    var {endName} = {cursorName}.Offset;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = new global::Parlot.TextSpan({scannerName}.Buffer, {startName}, {endName} - {startName});");
        }
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }
}
