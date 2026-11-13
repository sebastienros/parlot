using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated copies of the grammars of LongestBenchmarks
public partial class LongestBenchmarks
{
    [GenerateParser(nameof(TryParseOneOf))]
    private static Parser<int> BuildOneOf() => ZeroOrMany(Parsers.OneOf(
        Terms.Text("<<=").Then(static _ => 1),
        Terms.Text("<<").Then(static _ => 2),
        Terms.Text("<=").Then(static _ => 3),
        Terms.Text("<").Then(static _ => 4),
        Terms.Text("+=").Then(static _ => 5),
        Terms.Text("+").Then(static _ => 6),
        Terms.Char(';').Then(static _ => 7),
        Terms.Keyword("in").Then(static _ => 8),
        Terms.Identifier().Then(static _ => 9),
        Terms.Decimal().Then(static _ => 10))).Eof().Then(static tokens => Count(tokens));

    [GenerateParser(nameof(TryParseLongest))]
    private static Parser<int> BuildLongest() => ZeroOrMany(Parsers.Longest(
        Terms.Text("<").Then(static _ => 4),
        Terms.Text("<=").Then(static _ => 3),
        Terms.Text("<<").Then(static _ => 2),
        Terms.Text("<<=").Then(static _ => 1),
        Terms.Text("+").Then(static _ => 6),
        Terms.Text("+=").Then(static _ => 5),
        Terms.Char(';').Then(static _ => 7),
        Terms.Keyword("in").Then(static _ => 8),
        Terms.Identifier().Then(static _ => 9),
        Terms.Integer().Then(static _ => 10),
        Terms.Decimal().Then(static _ => 10))).Eof().Then(static tokens => Count(tokens));

    [GenerateParser(nameof(TryParseExclusiveOneOf))]
    private static Parser<int> BuildExclusiveOneOf() => ZeroOrMany(Parsers.OneOf(
        Terms.Char(';').Then(static _ => 7),
        Terms.Char('<').Then(static _ => 4),
        Terms.Identifier().Then(static _ => 9),
        Terms.Integer().Then(static _ => 10))).Eof().Then(static tokens => Count(tokens));

    [GenerateParser(nameof(TryParseExclusiveLongest))]
    private static Parser<int> BuildExclusiveLongest() => ZeroOrMany(Parsers.Longest(
        Terms.Char(';').Then(static _ => 7),
        Terms.Char('<').Then(static _ => 4),
        Terms.Identifier().Then(static _ => 9),
        Terms.Integer().Then(static _ => 10))).Eof().Then(static tokens => Count(tokens));
}
