using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class LookaheadBenchmarks
{
    [GenerateParser(nameof(TryParseFollowed))]
    private static Parser<string> BuildFollowed() =>
        Terms.Identifier().WhenFollowedBy(Terms.Char('(')).Then(static value => "");

    [GenerateParser(nameof(TryParseNotFollowed))]
    private static Parser<string> BuildNotFollowed() =>
        Terms.Identifier().WhenNotFollowedBy(Terms.Char('(')).Then(static value => "");

    [GenerateParser(nameof(TryParseNot))]
    private static Parser<string> BuildNot() =>
        Not(Terms.Text("from")).SkipAnd(Terms.Identifier()).Then(static value => "");

    [GenerateParser(nameof(TryParseWhen))]
    private static Parser<string> BuildWhen() =>
        Terms.Identifier().When(static (_, value) => value.Length == 0 || value.Span[0] != 'f').Then(static value => "");
}
