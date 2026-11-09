using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class SequenceStartBenchmarks
{
    [GenerateParser(nameof(TryParseClass))]
    private static Parser<int> BuildClass() =>
        ZeroOrMany(Terms.Char('@').SkipAnd(Terms.Identifier())).And(Terms.Text("class")).And(Terms.Identifier())
            .Then(static (context, start, end, _) => end - start);
}
