#nullable enable

using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Interpolated texts, compared with the runtime parsers of InterpolatedTests, which repeats the grammars below.
public static partial class InterpolatedGrammars
{
    // A JavaScript template literal whose holes hold identifiers or other templates, the chunks between brackets
    private static Parser<string> Template(InterpolationOptions options)
    {
        var expression = Deferred<string>();
        var template = Terms.Interpolated("`", "`", "${", "}", expression, options).Then(static x =>
        {
            var result = "[" + x.Chunks[0].ToString() + "]";
            for (var i = 0; i < x.Holes.Count; i++)
            {
                result += "{" + x.Holes[i] + "}[" + x.Chunks[i + 1].ToString() + "]";
            }
            return result;
        });
        expression.Parser = OneOf(Terms.Identifier().Then(static x => x.ToString()), template);
        return template;
    }

    [GenerateParser(nameof(TryParseTemplates))]
    private static Parser<IReadOnlyList<string>> BuildTemplates() => ZeroOrMany(Template(InterpolationOptions.None)).Eof();

    [GenerateParser(nameof(TryParseTightTemplates))]
    private static Parser<IReadOnlyList<string>> BuildTightTemplates() => ZeroOrMany(Template(InterpolationOptions.NoWhiteSpaceBeforeHoleClose)).Eof();

    // The value isn't used: the chunks and holes aren't listed
    [GenerateParser(nameof(TryParseCapturedTemplates))]
    private static Parser<int> BuildCapturedTemplates() => Capture(ZeroOrMany(Template(InterpolationOptions.None))).Then(static x => x.Length).Eof();

    [GenerateParser(nameof(TryParseStrings))]
    private static Parser<IReadOnlyList<int>> BuildStrings() =>
        ZeroOrMany(Terms.Interpolated("$\"", "\"", "{", "}", Terms.Identifier(), InterpolationOptions.NoNewLines).Then(static x => x.Chunks.Count * 100 + x.Chunks[x.Chunks.Count - 1].Length)).Eof();

    [GenerateParser(nameof(TryParseLongDelimiters))]
    private static Parser<IReadOnlyList<int>> BuildLongDelimiters() =>
        ZeroOrMany(Terms.Interpolated("<<", ">>", "#[[", "]]", Literals.Identifier(), escape: '\0').Then(static x => x.Holes.Count * 100 + x.Chunks[0].Length)).Eof();

    // The holes are TextSpans, like the chunks
    [GenerateParser(nameof(TryParseSpanHoles))]
    private static Parser<IReadOnlyList<string>> BuildSpanHoles() =>
        ZeroOrMany(Terms.Interpolated("`", "`", "${", "}", Terms.Identifier()).Then(static x =>
        {
            var result = x.Chunks[0].ToString();
            for (var i = 0; i < x.Holes.Count; i++)
            {
                result += "{" + x.Holes[i].ToString() + "}" + x.Chunks[i + 1].ToString();
            }
            return result;
        })).Eof();
}
