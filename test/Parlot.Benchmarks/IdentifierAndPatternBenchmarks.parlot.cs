using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class IdentifierAndPatternBenchmarks
{
    [GenerateParser(nameof(TryParseIdentifier))]
    private static Parser<int> BuildIdentifier() =>
        ZeroOrMany(Terms.Identifier()).Then(static tokens => tokens.Count);

    [GenerateParser(nameof(TryParseIdentifierWithPredicates))]
    private static Parser<int> BuildIdentifierWithPredicates() =>
        ZeroOrMany(Terms.Identifier(static c => c == '@', static c => c == '-')).Then(static tokens => tokens.Count);

    [GenerateParser(nameof(TryParsePattern))]
    private static Parser<int> BuildPattern() =>
        ZeroOrMany(Terms.Pattern(static c => Character.IsIdentifierPart(c) || c == '-')).Then(static tokens => tokens.Count);
}
