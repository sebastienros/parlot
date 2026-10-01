using System;
using System.Collections.Generic;
using System.Linq;
using Parlot.Fluent;

namespace Parlot.SourceGeneration;

internal static class TextChoiceSource
{
    internal static SourceResult? TryGenerate<T>(IReadOnlyList<Parser<T>> parsers, bool skipWhitespace, SourceGenerationContext context)
    {
        var texts = new List<string>();
        if (!Collect(parsers) || texts.Count < 8 || texts.Count > 256 || texts.Sum(static text => text.Length) > 4096)
        {
            return null;
        }

        var result = new SourceResult("success", "value", SourceGenerationContext.GetTypeName(typeof(string)));
        var cursor = context.CursorName;
        var body = result.Body;
        if (skipWhitespace)
        {
            body.Add($"var start = {cursor}.Position;");
            body.Add($"{context.ParseContextName}.SkipWhiteSpace();");
        }

        // A shorter remainder could still be the prefix of a text, or of a longer alternative.
        body.Add($"var textSpan = {cursor}.Span;");
        body.Add($"if (textSpan.Length < {texts.Max(static text => text.Length)}) {cursor}.MarkHitEnd();");
        body.Add("var text = MatchText(textSpan);");
        body.Add("if (text != null)");
        body.Add("{");
        var newLineGroups = texts.Distinct(StringComparer.Ordinal)
            .Select(static text => (Text: text, NewLines: TextLiteral.CountNewLines(text), Trailing: TextLiteral.TrailingSegmentLength(text)))
            .Where(static text => text.NewLines > 0)
            .GroupBy(static text => (text.NewLines, text.Trailing)).ToArray();
        if (newLineGroups.Length == 0)
        {
            body.Add($"    {cursor}.AdvanceBy(text.Length, 0, text.Length);");
        }
        else
        {
            body.Add("    switch (text)");
            body.Add("    {");
            foreach (var group in newLineGroups)
            {
                foreach (var literal in group)
                {
                    body.Add($"        case {LiteralHelper.StringToLiteral(literal.Text)}:");
                }

                body.Add($"            {cursor}.AdvanceBy(text.Length, {group.Key.NewLines}, {group.Key.Trailing});");
                body.Add("            break;");
            }

            body.Add("        default:");
            body.Add($"            {cursor}.AdvanceBy(text.Length, 0, text.Length);");
            body.Add("            break;");
            body.Add("    }");
        }

        body.Add("    value = text;");
        body.Add("    return true;");
        body.Add("}");
        if (skipWhitespace)
        {
            body.Add($"{cursor}.ResetPosition(start);");
        }

        body.Add("value = default;");
        body.Add("return false;");
        body.Add("static string MatchText(System.ReadOnlySpan<char> text)");
        body.Add("{");
        body.Add(KnownStringLookup.GeneratePrefixes(texts));
        body.Add("}");
        return result;

        bool Collect(IReadOnlyList<Parser<T>> choices)
        {
            foreach (var parser in choices)
            {
                if (parser is OneOf<T> oneOf && oneOf.SkipWhitespace == skipWhitespace)
                {
                    if (!Collect(oneOf.Parsers))
                    {
                        return false;
                    }
                }
                else if (parser is TextLiteral text && text.Comparison == StringComparison.Ordinal
                    && text.Text.Length is > 0 and <= 64)
                {
                    texts.Add(text.Text);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    }
}
