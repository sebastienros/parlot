using System;
using BenchmarkDotNet.Attributes;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public partial class GeneratedStringBenchmarks
{
    [Params("%hello%", "%hello\\nworld%")]
    public string Input { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!TryParseCaptured(Input, out var captured) || captured != Input.Length
            || !TryParseDecoded(Input, out var decoded) || decoded != (Input.Contains("\\n", StringComparison.Ordinal) ? 11 : 5)
            || TryParseCaptured("%unterminated", out _) || TryParseDecoded("%unterminated", out _))
        {
            throw new InvalidOperationException("Unexpected generated string result.");
        }
    }

    [Benchmark]
    public int Captured()
    {
        _ = TryParseCaptured(Input, out var value);
        return value;
    }

    [Benchmark]
    public int Decoded()
    {
        _ = TryParseDecoded(Input, out var value);
        return value;
    }

    private static partial bool TryParseCaptured(string input, out int value);
    private static partial bool TryParseDecoded(string input, out int value);
}
