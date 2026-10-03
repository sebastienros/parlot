using System;
using BenchmarkDotNet.Attributes;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public partial class DiscardResultBenchmarks
{
    private const string Input = "KeYwOrDvalueEnD";

    [GlobalSetup]
    public void Setup()
    {
        if (!TryParseSkipped(Input, out var skipped) || skipped != "value"
            || !TryParseBetween(Input, out var between) || between != "value"
            || !TryParseRetained(Input, out var retained) || retained != "KeYwOrD")
        {
            throw new InvalidOperationException("Incorrect discarded text parsing.");
        }
    }

    [Benchmark]
    public string SkippedText() => TryParseSkipped(Input, out var value) ? value : null;

    [Benchmark]
    public string BetweenText() => TryParseBetween(Input, out var value) ? value : null;

    [Benchmark]
    public string RetainedText() => TryParseRetained(Input, out var value) ? value : null;

    private static partial bool TryParseSkipped(string text, out string value);
    private static partial bool TryParseBetween(string text, out string value);
    private static partial bool TryParseRetained(string text, out string value);
}
