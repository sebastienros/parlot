using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Tests.Json;

namespace Parlot.Benchmarks;

/// <summary>
/// The cost of returning the furthest failure from a generated entry point: the overloads share the parser.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public partial class FailurePositionBenchmarks
{
#nullable disable
    private string _valid;
    private string _invalid;
#nullable restore

    [GlobalSetup]
    public void Setup()
    {
        _valid = JsonBench.BuildJson(4, 4, 3).ToString()!;

        // A missing comma near the end of the document
        var comma = _valid.LastIndexOf(',');
        _invalid = _valid.Remove(comma, 1);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Success")]
    public bool SuccessDefault() => TryParse(_valid, out _);

    [Benchmark, BenchmarkCategory("Success")]
    public bool SuccessPosition() => TryParse(_valid, out _, out (int, int, int) _);

    [Benchmark(Baseline = true), BenchmarkCategory("Failure")]
    public bool FailureDefault() => TryParse(_invalid, out _);

    [Benchmark, BenchmarkCategory("Failure")]
    public int FailureOffset() => TryParse(_invalid, out _, out int offset) ? -1 : offset;

    [Benchmark, BenchmarkCategory("Failure")]
    public int FailurePosition() => TryParse(_invalid, out _, out (int Offset, int Line, int Column) failure) ? -1 : failure.Line;

    private static partial bool TryParse(string input, out IJson value);
    private static partial bool TryParse(string input, out IJson value, out int failureOffset);
    private static partial bool TryParse(string input, out IJson value, out (int Offset, int Line, int Column) failure);
}
