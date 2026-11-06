using BenchmarkDotNet.Attributes;

namespace Parlot.Benchmarks;

// A lookahead saves and restores the furthest failure, and a rejected value computes where it starts on its failure path
[MemoryDiagnoser, ShortRunJob]
public partial class LookaheadBenchmarks
{
    public static partial bool TryParseFollowed(string text, out string value);
    public static partial bool TryParseNotFollowed(string text, out string value);
    public static partial bool TryParseNot(string text, out string value);
    public static partial bool TryParseWhen(string text, out string value);

    [Benchmark]
    public bool Followed() => TryParseFollowed("  word  (", out _);

    [Benchmark]
    public bool NotFollowed() => TryParseNotFollowed("  word  ;", out _);

    [Benchmark]
    public bool Negative() => TryParseNot("  word", out _);

    [Benchmark]
    public bool WhenAccepted() => TryParseWhen("  word", out _);

    [Benchmark]
    public bool WhenRejected() => TryParseWhen("  from", out _);
}
