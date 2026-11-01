using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;

namespace Parlot.Benchmarks;

/// <summary>
/// Chains of prefix operators of growing length, with the grammars of <see cref="PrefixTableBenchmarks"/>. The operators
/// of a chain are read in a loop, and applied from the last one once the operand is parsed.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PrefixChainBenchmarks
{
    private Parser<long> _unary = null!;
    private Parser<long> _table = null!;
    private string _input = "";

    [Params(1, 2, 5, 8, 32)]
    public int Count { get; set; } = 5;

    // The operators "-", "~" and "!" in turn, then the operand 42
    public static string GetInput(int count)
    {
        var chars = new char[count * 2];

        for (var i = 0; i < count; i++)
        {
            chars[i * 2] = "-~!"[i % 3];
            chars[i * 2 + 1] = ' ';
        }

        return new string(chars) + "42";
    }

    [GlobalSetup]
    public void Setup()
    {
        _unary = PrefixTableGrammar.Create(operatorTable: false);
        _table = PrefixTableGrammar.Create(operatorTable: true);
        _input = GetInput(Count);

        var expected = UnaryRuntime();

        if (TableRuntime() != expected || UnaryGenerated() != expected || TableGenerated() != expected)
        {
            throw new InvalidOperationException("Prefix chain benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Runtime")]
    public long UnaryRuntime() => _unary.Parse(_input);

    [Benchmark, BenchmarkCategory("Runtime")]
    public long TableRuntime() => _table.Parse(_input);

    [Benchmark(Baseline = true), BenchmarkCategory("Generated")]
    public long UnaryGenerated() => PrefixTableBenchmarks.TryParseUnary(_input, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public long TableGenerated() => PrefixTableBenchmarks.TryParseTable(_input, out var value) ? value : -1;
}
