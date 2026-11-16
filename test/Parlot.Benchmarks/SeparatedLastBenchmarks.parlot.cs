using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class SeparatedLastBenchmarks
{
    [GenerateParser(nameof(TryParseSeparatedLast))]
    private static Parser<IReadOnlyList<string>> BuildSeparatedLast() =>
        Separated(Terms.Char(','), Terms.Identifier().Then(static x => x.ToString()), Terms.Text("...").SkipAnd(Terms.Identifier()).Then(static x => x.ToString()));
}
