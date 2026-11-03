using Parlot.Fluent;
using Parlot.SourceGenerator;

namespace Parlot.Benchmarks;

public partial class ThenOffsetsBenchmarks
{
    [GenerateParser(nameof(TryParseChoice))]
    private static Parser<int> BuildChoice() =>
        Parsers.Terms.Text("ab").Or(Parsers.Terms.Text("cd")).Then(static (context, start, end, _) => end - start);
}
