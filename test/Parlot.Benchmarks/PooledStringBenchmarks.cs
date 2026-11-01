using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method            | Scenario | Mean     | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
// |------------------ |--------- |---------:|---------:|---------:|------:|-------:|----------:|------------:|
// | Identifiers       | Distinct | 54.78 us | 8.183 us | 7.654 us |  1.02 | 3.2349 |   54.9 KB |        1.00 |
// | PooledIdentifiers | Distinct | 58.74 us | 7.199 us | 6.734 us |  1.09 | 3.2349 |   54.9 KB |        1.00 |
// | Strings           | Distinct | 86.86 us | 7.106 us | 5.934 us |  1.61 | 3.4180 |  58.41 KB |        1.06 |
// | PooledStrings     | Distinct | 86.72 us | 6.734 us | 5.623 us |  1.61 | 3.4180 |  58.41 KB |        1.06 |
// | Identifiers       | Repeated | 50.48 us | 2.914 us | 2.726 us |  1.00 | 3.2349 |   54.9 KB |        1.00 |
// | PooledIdentifiers | Repeated | 56.86 us | 3.034 us | 2.690 us |  1.13 | 0.4272 |   8.02 KB |        0.15 |
// | Strings           | Repeated | 84.23 us | 7.964 us | 7.450 us |  1.67 | 3.1738 |   54.9 KB |        1.00 |
// | PooledStrings     | Repeated | 90.40 us | 9.746 us | 9.116 us |  1.80 | 0.3662 |   8.02 KB |        0.15 |

// Measures ToPooledString() against ToString() on 1000 identifiers or string literals: "Repeated" uses 50 distinct
// values, "Distinct" 1000 different ones, which switch the pool off. The 8 KB left are the result list.

[MemoryDiagnoser, ShortRunJob]
public class PooledStringBenchmarks
{
    private readonly Parser<IReadOnlyList<string>> _identifiers = ZeroOrMany(Terms.Identifier().Then(static x => x.ToString())).Eof();
    private readonly Parser<IReadOnlyList<string>> _pooledIdentifiers = ZeroOrMany(Terms.Identifier().ToPooledString()).Eof();
    private readonly Parser<IReadOnlyList<string>> _strings = ZeroOrMany(Terms.String().Then(static x => x.ToString())).Eof();
    private readonly Parser<IReadOnlyList<string>> _pooledStrings = ZeroOrMany(Terms.String().ToPooledString()).Eof();

    private string _identifierInput = "";
    private string _stringInput = "";

    [Params("Repeated", "Distinct")]
    public string Scenario { get; set; } = "Repeated";

    [GlobalSetup]
    public void Setup()
    {
        var distinct = Scenario == "Distinct" ? 1000 : 50;
        var names = Enumerable.Range(0, 1000).Select(i => "identifier" + (i * 7919 % distinct)).ToArray();

        _identifierInput = string.Join(" ", names);
        // Half of the strings have an escape sequence
        _stringInput = string.Join(" ", names.Select(static (name, i) => i % 2 == 0 ? $"'{name}'" : $"'{name}\\n'"));

        if (!Identifiers().SequenceEqual(PooledIdentifiers()) || !Strings().SequenceEqual(PooledStrings()))
        {
            throw new InvalidOperationException("Incorrect parsing.");
        }
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> Identifiers() => _identifiers.Parse(_identifierInput)!;

    [Benchmark]
    public IReadOnlyList<string> PooledIdentifiers() => _pooledIdentifiers.Parse(_identifierInput)!;

    [Benchmark]
    public IReadOnlyList<string> Strings() => _strings.Parse(_stringInput)!;

    [Benchmark]
    public IReadOnlyList<string> PooledStrings() => _pooledStrings.Parse(_stringInput)!;
}
