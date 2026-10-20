using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method | Count | Mean       | Error    | StdDev    | Median     | Gen0   | Gen1   | Allocated |
// |------- |------ |-----------:|---------:|----------:|-----------:|-------:|-------:|----------:|
// | Build  | 4     |   587.8 ns | 11.68 ns |  20.15 ns |   578.6 ns | 0.4339 |      - |   3.99 KB |
// | Build  | 32    | 3,379.1 ns | 67.01 ns | 163.11 ns | 3,304.8 ns | 1.8425 | 0.0458 |  16.95 KB |

// Measures building a OneOf whose alternatives are seekable, which builds its lookup table.

[MemoryDiagnoser, ShortRunJob]
public class OneOfConstructionBenchmarks
{
    private Parser<string>[] _parsers;

    [Params(4, 32)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _parsers = Enumerable.Range(0, Count).Select(static i => Literals.Text($"{(char)('a' + i % 26)}{i}")).ToArray();
    }

    [Benchmark]
    public Parser<string> Build() => new OneOf<string>(_parsers);
}
