using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class CancellationBenchmarks
{
    [GenerateParser(nameof(TryParse))]
    private static Parser<int> BuildCancelable() => OneOrMany(Literals.Char('x')).Then(static values => values.Count).Eof();

    [GenerateParser(nameof(TryParseWithoutToken))]
    private static Parser<int> BuildWithoutToken() => BuildCancelable();
}
