using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Measures ToPooledString() against ToString() on 1000 identifiers or string literals: "Repeated" uses 50 distinct
// values, "Distinct" 1000 different ones, which switch the pool off. Results are added in the pull request.

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
