using BenchmarkDotNet.Attributes;

namespace Parlot.Benchmarks;

// Before the shared single chars:
// | Method   | Mean      | Error     | StdDev    | Gen0   | Allocated |
// |--------- |----------:|----------:|----------:|-------:|----------:|
// | Ascii    | 10.735 ns | 0.4682 ns | 0.3655 ns | 0.0014 |      24 B |
// | NonAscii |  9.959 ns | 0.4216 ns | 0.3521 ns | 0.0014 |      24 B |
// | Eight    | 13.359 ns | 0.6010 ns | 0.4692 ns | 0.0023 |      40 B |
//
// After:
// | Method   | Mean      | Error     | StdDev    | Gen0   | Allocated |
// |--------- |----------:|----------:|----------:|-------:|----------:|
// | Ascii    |  1.382 ns | 0.1343 ns | 0.1121 ns |      - |         - |
// | NonAscii | 12.122 ns | 0.9592 ns | 0.8503 ns | 0.0014 |      24 B |
// | Eight    | 13.501 ns | 1.3349 ns | 1.2487 ns | 0.0023 |      40 B |

// Measures TextSpan.ToString() on a single ASCII char, a single other char, and 8 chars. Single ASCII chars return a
// shared string.

[MemoryDiagnoser, ShortRunJob]
public class TextSpanToStringBenchmarks
{
    private readonly TextSpan _ascii = new("a+b", 1, 1);
    private readonly TextSpan _nonAscii = new("aéb", 1, 1);
    private readonly TextSpan _eight = new("(abcdefgh)", 1, 8);

    [Benchmark]
    public string Ascii() => _ascii.ToString();

    [Benchmark]
    public string NonAscii() => _nonAscii.ToString();

    [Benchmark]
    public string Eight() => _eight.ToString();
}
