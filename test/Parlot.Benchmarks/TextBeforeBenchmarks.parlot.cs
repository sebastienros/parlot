using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class TextBeforeBenchmarks
{
    // The same parser as the runtime benchmark, returning the length of the text before the delimiter
    [GenerateParser(nameof(TryParseFalseStarts))]
    private static Parser<int> BuildFalseStarts() =>
        AnyCharBefore(Literals.Text("-->").Or(Literals.Text("==>"))).Then(static span => span.Length);
}
