using BenchmarkDotNet.Attributes;
using System;

namespace Parlot.Benchmarks;

// A choice of keywords whose words also take every char above ASCII, a set of identifier chars built with the grammar,
// against the same keywords written as scanners, one after the other, which was the way to get this boundary before.
[MemoryDiagnoser, ShortRunJob]
public partial class KeywordLargeSetBenchmarks
{
    private const string Input = "ORDER";
    private const string Rejected = "orderé";

    public static partial bool TryParseKeywords(string text, out string value);
    public static partial bool TryParseScans(string text, out int value);

    private static bool IsWordPart(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '$' or >= '\u0080' and <= '￾';

    internal static int ScanKeyword(ReadOnlySpan<char> span, string keyword)
    {
        if (span.Length < keyword.Length || !span.Slice(0, keyword.Length).Equals(keyword.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return span.Length > keyword.Length && IsWordPart(span[keyword.Length]) ? 0 : keyword.Length;
    }

    [Benchmark(Baseline = true)]
    public bool Scans() => TryParseScans(Input, out _);

    [Benchmark]
    public bool Keywords() => TryParseKeywords(Input, out _);

    [Benchmark]
    public bool ScansRejected() => TryParseScans(Rejected, out _);

    [Benchmark]
    public bool KeywordsRejected() => TryParseKeywords(Rejected, out _);
}
