using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;


// | Method              | Length | Mean     | Error    | StdDev   | Gen0   | Allocated |
// |-------------------- |------- |---------:|---------:|---------:|-------:|----------:|
// | LookupMatchFluent   | 2      | 25.47 ns | 6.780 ns | 0.372 ns | 0.0091 |     152 B |
// | LookupMatchCompiled | 2      | 39.50 ns | 5.996 ns | 0.329 ns | 0.0091 |     152 B |
// | LookupMissFluent    | 2      | 20.71 ns | 5.622 ns | 0.308 ns | 0.0091 |     152 B |
// | LookupMissCompiled  | 2      | 26.30 ns | 5.586 ns | 0.306 ns | 0.0091 |     152 B |
// | LookupMatchFluent   | 255    | 24.55 ns | 1.340 ns | 0.073 ns | 0.0091 |     152 B |
// | LookupMatchCompiled | 255    | 39.80 ns | 0.845 ns | 0.046 ns | 0.0091 |     152 B |
// | LookupMissFluent    | 255    | 24.83 ns | 8.213 ns | 0.450 ns | 0.0091 |     152 B |
// | LookupMissCompiled  | 255    | 27.73 ns | 4.930 ns | 0.270 ns | 0.0091 |     152 B |

// Lookups win all the time, switches, loops,are no match.
// The compiled code is slower, even when using the Compiled wrapper over the fluent implementation.

[MemoryDiagnoser, ShortRunJob]
public class SwitchExpressionBenchmarks
{
    private Parser<char> _fluent;
    private Parser<char> _compiled;
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
        _compiled = _fluent.Compile();
        _matchString = ((CharLiteral)parsers[(int)(Length * 0.7)]).Char.ToString();
        _missString = ((char)MaxValue).ToString();
    }

    [Benchmark]
    public char LookupMatchFluent()
    {
        return _fluent.Parse(_matchString);
    }

    [Benchmark]
    public char LookupMatchCompiled()
    {
        return _compiled.Parse(_matchString);
    }

    [Benchmark]
    public char LookupMissFluent()
    {
        return _fluent.Parse(_missString);
    }

    [Benchmark]
    public char LookupMissCompiled()
    {
        return _compiled.Parse(_missString);
    }
}
