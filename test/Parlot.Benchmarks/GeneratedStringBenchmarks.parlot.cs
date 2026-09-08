using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class GeneratedStringBenchmarks
{
    [GenerateParser(nameof(TryParseCaptured))]
    private static Parser<int> BuildCaptured() =>
        Capture(new StringLiteral('%')).Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseDecoded))]
    private static Parser<int> BuildDecoded() =>
        new StringLiteral('%').Then(static span => span.Length).Eof();
}
