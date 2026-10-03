using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class DiscardResultBenchmarks
{
    [GenerateParser(nameof(TryParseSkipped))]
    private static Parser<string> BuildSkipped() =>
        Literals.Text("keyword", caseInsensitive: true, returnMatchedText: true)
            .SkipAnd(Literals.Text("value"))
            .AndSkip(Literals.Text("end", caseInsensitive: true, returnMatchedText: true)).Eof();

    [GenerateParser(nameof(TryParseBetween))]
    private static Parser<string> BuildBetween() =>
        Between(
            Literals.Text("keyword", caseInsensitive: true, returnMatchedText: true),
            Literals.Text("value"),
            Literals.Text("end", caseInsensitive: true, returnMatchedText: true)).Eof();

    [GenerateParser(nameof(TryParseRetained))]
    private static Parser<string> BuildRetained() =>
        Literals.Text("keyword", caseInsensitive: true, returnMatchedText: true);
}
