using BenchmarkDotNet.Attributes;

namespace Parlot.Benchmarks;

// A callback with offsets on a choice of Terms parsers, whose start is read after the white space. Only these callbacks
// pay the check for white space, once more than the alternatives do.
[MemoryDiagnoser, ShortRunJob]
public partial class ThenOffsetsBenchmarks
{
    public static partial bool TryParseChoice(string text, out int value);

    [Params("cd", "  cd")]
    public string Input { get; set; } = "";

    [Benchmark]
    public bool Choice() => TryParseChoice(Input, out _);
}
