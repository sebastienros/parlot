#nullable enable

using System;
using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated copies of the runtime grammars of LongestTests
public static partial class LongestGrammars
{
    [GenerateParser(nameof(TryParseNumbers))]
    public static Parser<IReadOnlyList<string>> BuildNumbers() => ZeroOrMany(Longest(
        Terms.Integer().Then(static x => "i" + x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Terms.Decimal().Then(static x => "d" + x.ToString(System.Globalization.CultureInfo.InvariantCulture)))).Eof();

    [GenerateParser(nameof(TryParseStatements))]
    public static Parser<IReadOnlyList<string>> BuildStatements() => ZeroOrMany(Longest(
        Terms.Identifier().And(ZeroOrOne(Terms.Text(":=").SkipAnd(Terms.Integer()))).Then(static x => "e:" + x.Item1.ToString()),
        Terms.Identifier().AndSkip(Terms.Char(':')).Then(static x => "l:" + x.ToString()))).Eof();

    [GenerateParser(nameof(TryParseTokens))]
    public static Parser<IReadOnlyList<string>> BuildTokens() => ZeroOrMany(Longest(
        Terms.Text("<"), Terms.Text("<="), Terms.Text("<<"), Terms.Text("<<="),
        Terms.Keyword("in"),
        Terms.Identifier().Then(static x => "id:" + x.ToString()),
        Terms.Integer().Then(static x => "int:" + x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Terms.Decimal().Then(static x => "dec:" + x.ToString(System.Globalization.CultureInfo.InvariantCulture)))).Eof();

    // Alternatives which skip white space and others which don't, one which can start with any char and one which can
    // start with any char above ASCII
    [GenerateParser(nameof(TryParseMixed))]
    public static Parser<IReadOnlyList<string>> BuildMixed() => ZeroOrMany(OneOf(
        Longest(Literals.Text("ab"), Literals.Text("abc"), Literals.Pattern(static c => c == 'a' || c == 'x').Then(static x => "p:" + x.ToString()),
            Literals.Scan(static span => span.Length > 0 && span[0] >= '\u0080' ? 1 : 0, "éèêëàâäôöûüçîïÿñæœåøßþðáíóúýāēīōūăĕĭŏŭąęįųćčďěňřšťžźżłńśőűđħıĳľŀŉŋŧſ".AsSpan()).Then(static x => "u:" + x.ToString())),
        Longest(Terms.Text("cd"), Literals.Text(" c")),
        Literals.Char(' ').Then(static _ => "_"))).Eof();

    // Keywords win over the identifiers of the same length
    [GenerateParser(nameof(TryParseKeywords))]
    public static Parser<IReadOnlyList<string>> BuildKeywords() => ZeroOrMany(Longest(static matches =>
    {
        var best = matches.IndexOfLongest();

        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].End == matches[best].End && matches[i].Alternative == 1)
            {
                return i;
            }
        }

        return best;
    }, Terms.Identifier().Then(static x => "id:" + x.ToString()), Terms.Keyword("class").Then(static _ => "class"))).Eof();

    // The shortest match, or none when it starts with a 'b'
    [GenerateParser(nameof(TryParseShortest))]
    public static Parser<IReadOnlyList<string>> BuildShortest() => ZeroOrMany(Longest(static (ParseContext context, LongestMatches<string> matches) =>
        matches[0].Value.StartsWith("b", StringComparison.Ordinal) ? -1 : 0,
        Terms.Text("ab"), Terms.Text("abab"), Terms.Text("ababab"), Terms.Text("b"))).Eof();

    [GenerateParser(nameof(TryParseWarnings))]
    public static Parser<string> BuildWarnings() => Longest(
        Terms.Text("ab").Warning("short"),
        Terms.Text("abc").Warning("long"),
        Terms.Text("abcd").ElseError("unused"),
        Terms.Text("x"));

    [GenerateParser(nameof(TryParseSelectedWarnings))]
    public static Parser<string> BuildSelectedWarnings() => Longest(static matches => 0,
        Terms.Text("ab").Warning("short"),
        Terms.Text("abc").Warning("long"),
        Terms.Text("x"));

    // The value of the choice isn't used
    [GenerateParser(nameof(TryParseCaptured))]
    public static Parser<string> BuildCaptured() => Capture(ZeroOrMany(Longest(Literals.Text("a"), Literals.Text("ab"), Literals.Text("abc"), Literals.Text(",")))).Then(static x => x.ToString());

    // Alternatives which fail at different depths
    [GenerateParser(nameof(TryParseDepths))]
    public static Parser<string> BuildDepths() => Longest(
        Terms.Text("ab").And(Terms.Text("cd")).Then(static x => x.Item1 + x.Item2),
        Terms.Text("ab").And(Terms.Text("c")).And(Terms.Text("ef")).Then(static x => x.Item1 + x.Item2 + x.Item3),
        Terms.Text("x"));
}
