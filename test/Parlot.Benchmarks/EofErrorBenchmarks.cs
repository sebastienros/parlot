using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser]
public class EofErrorBenchmarks
{
    private static readonly Parser<long> _eof = Terms.Integer().Eof();
    private static readonly Parser<long> _error = Terms.Integer().Eof().ElseError("Invalid token in expression");
    private static readonly Parser<long> _fallback = Terms.Integer().Eof().Or(Terms.Integer());

    [Benchmark(Baseline = true)]
    public long Eof() => _eof.Parse("42");

    [Benchmark]
    public long EofElseError() => _error.Parse("42");

    [Benchmark]
    public long EofFallback() => _fallback.Parse("42a");
}
