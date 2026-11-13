using System;
using System.Collections.Generic;
using System.Linq;
using Parlot.Fluent;

namespace Parlot.SourceGeneration;

internal static class KeywordChoiceSource
{
    internal static SourceResult? TryGenerate<T>(IReadOnlyList<Parser<T>> parsers, bool skipWhitespace, SourceGenerationContext context)
    {
        var keywords = new List<string>();
        KeywordLiteral? first = null;
        if (!Collect(parsers) || keywords.Count < 8 || keywords.Count > 256 || keywords.Sum(static text => text.Length) > 4096)
        {
            return null;
        }

        var result = new SourceResult("success", "value", SourceGenerationContext.GetTypeName(typeof(string)));
        var cursor = context.CursorName;
        var body = result.Body;
        if (skipWhitespace)
        {
            body.Add($"var start = {cursor}.Offset;");
            body.Add($"{context.ParseContextName}.SkipWhiteSpace();");
        }

        body.Add($"var keywordInput = {cursor}.Span;");
        body.Add("var length = 0;");
        // An overlong identifier cannot match. Do not scan an unbounded unknown token.
        if (first!.HasCustomIdentifierPart)
        {
            body.Add($"while (length < keywordInput.Length && length <= {keywords.Max(static text => text.Length)})");
            body.Add("{");
            body.Add("    var keywordNext = keywordInput[length];");
            body.Add($"    if (!{first.GenerateIsIdentifierPart("keywordNext")}) break;");
            body.Add("    length++;");
            body.Add("}");
        }
        else
        {
            body.Add($"while (length < keywordInput.Length && length <= {keywords.Max(static text => text.Length)} && unchecked((uint)((keywordInput[length] | 0x20) - 'a')) <= 'z' - 'a')");
            body.Add("{");
            body.Add("    length++;");
            body.Add("}");
        }

        // The word could continue after the end of a partial buffer.
        body.Add($"if (length == keywordInput.Length) {cursor}.MarkHitEnd();");
        body.Add("var keyword = MatchKeyword(keywordInput.Slice(0, length));");
        body.Add("if (keyword != null)");
        body.Add("{");
        body.Add($"    {cursor}.AdvanceNoNewLines(length);");
        body.Add("    value = keyword;");
        body.Add("    return true;");
        body.Add("}");
        if (skipWhitespace)
        {
            body.Add($"{cursor}.ResetPosition(start);");
        }

        body.Add("value = default;");
        body.Add("return false;");
        body.Add("static string MatchKeyword(System.ReadOnlySpan<char> input)");
        body.Add("{");
        body.Add(KnownStringLookup.Generate(keywords, bytes: false, ignoreCase: first.Comparison == StringComparison.OrdinalIgnoreCase,
            matchExpression: index => LiteralHelper.StringToLiteral(keywords[index]), failureExpression: "null", resultType: "string"));
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
                // The recognizer scans the run of identifier chars and looks it up, so the keywords share their boundary
                // and comparison, and their own chars are part of the run. A case-insensitive lookup returns the canonical
                // text, so it can't return the matched one.
                else if (parser is KeywordLiteral keyword
                    && (keyword.Comparison == StringComparison.Ordinal
                        || (keyword.Comparison == StringComparison.OrdinalIgnoreCase && !keyword.ReturnMatchedText))
                    && (first ??= keyword).Comparison == keyword.Comparison
                    && first.HasCustomIdentifierPart == keyword.HasCustomIdentifierPart
                    && first.HasSameIdentifierPart(keyword)
                    && keyword.Text.Length is > 0 and <= 64
                    && keyword.Text.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' && keyword.IsIdentifierPart(c)))
                {
                    keywords.Add(keyword.Text);
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
