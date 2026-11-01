using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// Prefix operators applied to an integer, with Unary (Unary) or an Operators() table of only a prefix level (Table), at
/// runtime and generated. No operator can follow the operand of the table.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public partial class PrefixTableBenchmarks
{
    private Parser<long> _unary = null!;
    private Parser<long> _table = null!;
    private string _input = "";

    [Params("Operand", "Prefixes")]
    public string Input { get; set; } = "Prefixes";

    public static string GetInput(string name) => name switch
    {
        "Operand" => "42",
        "Prefixes" => "- ~ ! - ~42",
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [GlobalSetup]
    public void Setup()
    {
        _unary = PrefixTableGrammar.Create(operatorTable: false);
        _table = PrefixTableGrammar.Create(operatorTable: true);
        _input = GetInput(Input);

        var expected = UnaryRuntime();

        if (TableRuntime() != expected || UnaryGenerated() != expected || TableGenerated() != expected)
        {
            throw new InvalidOperationException("Prefix table benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Runtime")]
    public long UnaryRuntime() => _unary.Parse(_input);

    [Benchmark, BenchmarkCategory("Runtime")]
    public long TableRuntime() => _table.Parse(_input);

    [Benchmark(Baseline = true), BenchmarkCategory("Generated")]
    public long UnaryGenerated() => TryParseUnary(_input, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public long TableGenerated() => TryParseTable(_input, out var value) ? value : -1;

    public static partial bool TryParseUnary(string input, out long value);
    public static partial bool TryParseTable(string input, out long value);
}

/// <summary>
/// The runtime grammars of <see cref="PrefixTableBenchmarks"/>, built like the generated ones.
/// </summary>
public static class PrefixTableGrammar
{
    public static Parser<long> Create(bool operatorTable)
    {
        (Parser<char>, Func<long, long>)[] prefix =
        [
            (Terms.Char('-'), static x => -x),
            (Terms.Char('~'), static x => ~x),
            (Terms.Char('!'), static x => x == 0 ? 1 : 0),
        ];

        return operatorTable ? Terms.Integer().Operators().Unary(prefix) : Terms.Integer().Unary(prefix);
    }
}
