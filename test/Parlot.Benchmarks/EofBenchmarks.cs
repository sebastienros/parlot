using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser]
public class EofBenchmarks
{
    private static readonly Parser<long> _composed = Terms.Integer().AndSkip(Always().Eof());
    private static readonly Parser<long> _standalone = Terms.Integer().AndSkip(Eof());

    [Params("42", "42a")]
    public string Input { get; set; } = "42";

    [Benchmark(Baseline = true)]
    public bool Composed() => _composed.TryParse(Input, out _);

    [Benchmark]
    public bool Standalone() => _standalone.TryParse(Input, out _);
}
