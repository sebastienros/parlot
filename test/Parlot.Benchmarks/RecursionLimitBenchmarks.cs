using System;
using BenchmarkDotNet.Attributes;

namespace Parlot.Benchmarks;

/// <summary>
/// A generated parser with <c>MaxRecursionDepth</c> set, which counts the levels of its recursive parsers, against the same
/// grammar without a limit.
/// </summary>
[MemoryDiagnoser]
public partial class RecursionLimitBenchmarks
{
    private string _input = "";

    [Params("Flat", "Nested")]
    public string Input { get; set; } = "Nested";

    public static string GetInput(string name) => name switch
    {
        "Flat" => "1 + 2 * 3 - 4",
        "Nested" => "((1 + 2) * -(3 - (4 * 5))) + -(-(6 + (7 * (8 - 9))))",
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [GlobalSetup]
    public void Setup()
    {
        _input = GetInput(Input);

        if (Generated() != GeneratedWithLimit())
        {
            throw new InvalidOperationException("Recursion limit benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true)]
    public long Generated()
    {
        TryParse(_input, out var value);
        return value;
    }

    [Benchmark]
    public long GeneratedWithLimit()
    {
        TryParseLimited(_input, out var value);
        return value;
    }

    private static partial bool TryParse(string text, out long value);
    private static partial bool TryParseLimited(string text, out long value);
}
