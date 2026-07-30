using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;


// | Method              | Length | Mean     | Error    | StdDev   | Gen0   | Allocated |
// |-------------------- |------- |---------:|---------:|---------:|-------:|----------:|
// | LookupMatchFluent   | 2      | 25.47 ns | 6.780 ns | 0.372 ns | 0.0091 |     152 B |
// | LookupMissFluent    | 2      | 20.71 ns | 5.622 ns | 0.308 ns | 0.0091 |     152 B |
// | LookupMatchFluent   | 255    | 24.55 ns | 1.340 ns | 0.073 ns | 0.0091 |     152 B |
// | LookupMissFluent    | 255    | 24.83 ns | 8.213 ns | 0.450 ns | 0.0091 |     152 B |

// Lookups win all the time, switches, loops,are no match.

[MemoryDiagnoser, ShortRunJob]
public class SwitchExpressionBenchmarks
{
    private Parser<char> _fluent;
    private const int MaxValue = 127;
    private string _matchString;
    private string _missString;

    [Params(2, 255)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var parsers = Enumerable.Range(1, Length).Select(i => Literals.Char((char)(Random.Shared.Next(MaxValue-1)))).ToArray();
        _fluent = OneOf(parsers);
        _matchString = ((CharLiteral)parsers[(int)(Length * 0.7)]).Char.ToString();
        _missString = ((char)MaxValue).ToString();
    }

    [Benchmark]
    public char LookupMatchFluent()
    {
        return _fluent.Parse(_matchString);
    }

    [Benchmark]
    public char LookupMissFluent()
    {
        return _fluent.Parse(_missString);
    }

}
