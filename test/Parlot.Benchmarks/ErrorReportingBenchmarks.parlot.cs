using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class ErrorReportingBenchmarks
{
    [GenerateParser(nameof(TryParse))]
    private static Parser<char> BuildParser() => OneOf(
        Literals.Char('a'),
        Literals.Char('b').Error("unexpected b"));
}
