using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// An expression grammar whose loosest level is a right-associative conditional, like NCalc's: a Ternary level of the
/// Operators() table (Level), or an optional <c>? expression : expression</c> sequence after the table (Sequence), at
/// runtime and generated. Most inputs have no conditional, so they measure what the level costs when it isn't used.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public partial class TernaryLevelBenchmarks
{
    private Parser<long> _sequence = null!;
    private Parser<long> _level = null!;
    private string _input = "";

    [Params("Simple", "Calls", "Conditional")]
    public string Input { get; set; } = "Calls";

    public static string GetInput(string name) => name switch
    {
        "Simple" => "(3 < level && 5 >= level)",
        "Calls" => "state == 4 && a == 1 && f() == 1 || g(1 + 1, 1, 2, 3) && name == 4",
        "Conditional" => "a < 2 ? b + 1 : c == 3 ? d * 2 : e",
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [GlobalSetup]
    public void Setup()
    {
        _sequence = TernaryLevelGrammar.Create(level: false);
        _level = TernaryLevelGrammar.Create(level: true);
        _input = GetInput(Input);

        var expected = SequenceRuntime();

        if (LevelRuntime() != expected || SequenceGenerated() != expected || LevelGenerated() != expected)
        {
            throw new InvalidOperationException("Ternary level benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Runtime")]
    public long SequenceRuntime() => _sequence.Parse(_input);

    [Benchmark, BenchmarkCategory("Runtime")]
    public long LevelRuntime() => _level.Parse(_input);

    [Benchmark(Baseline = true), BenchmarkCategory("Generated")]
    public long SequenceGenerated() => TryParseSequence(_input, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public long LevelGenerated() => TryParseLevel(_input, out var value) ? value : -1;

    public static partial bool TryParseSequence(string input, out long value);
    public static partial bool TryParseLevel(string input, out long value);
}

/// <summary>
/// The runtime grammars of <see cref="TernaryLevelBenchmarks"/>, built like the generated ones.
/// </summary>
public static class TernaryLevelGrammar
{
    public static Parser<long> Create(bool level)
    {
        var expression = Deferred<long>();
        // The number of arguments of a call, which can have none
        var arguments = Between(Terms.Char('('), Separated(Terms.Char(','), expression).Optional(), Terms.Char(')'))
            .Then(static x => x.HasValue ? x.Value.Count : 0);
        var primary = OneOf(
            Terms.Integer(),
            Terms.Identifier().And(arguments.Optional()).Then(static x => (long)x.Item1.Length + (x.Item2.HasValue ? x.Item2.Value : 0)),
            Between(Terms.Char('('), expression, Terms.Char(')')));

        var table = primary.Operators()
            .Unary((Terms.Text("-"), static x => -x), (Terms.Text("!"), static x => x == 0 ? 1 : 0))
            .LeftAssociative((Terms.Text("*"), static (a, b) => a * b), (Terms.Text("/"), static (a, b) => b == 0 ? 0 : a / b))
            .LeftAssociative((Terms.Text("+"), static (a, b) => a + b), (Terms.Text("-"), static (a, b) => a - b))
            .LeftAssociative((Terms.Text("<="), static (a, b) => a <= b ? 1 : 0), (Terms.Text(">="), static (a, b) => a >= b ? 1 : 0), (Terms.Text("<"), static (a, b) => a < b ? 1 : 0), (Terms.Text(">"), static (a, b) => a > b ? 1 : 0))
            .LeftAssociative((Terms.Text("=="), static (a, b) => a == b ? 1 : 0), (Terms.Text("!="), static (a, b) => a != b ? 1 : 0))
            .LeftAssociative((Terms.Text("&&"), static (a, b) => a != 0 && b != 0 ? 1 : 0))
            .LeftAssociative((Terms.Text("||"), static (a, b) => a != 0 || b != 0 ? 1 : 0));

        if (level)
        {
            // A right-associative level of the table: the last operand can be another conditional
            expression.Parser = table.Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (c, a, b) => c != 0 ? a : b);
            return expression.Eof();
        }

        // The conditional is an optional sequence after the table, whose last operand is the whole expression
        expression.Parser = table
            .And(Terms.Char('?').SkipAnd(expression).AndSkip(Terms.Char(':')).And(expression).Optional())
            .Then(static x => x.Item2.HasValue ? (x.Item1 != 0 ? x.Item2.Value.Item1 : x.Item2.Value.Item2) : x.Item1);

        return expression.Eof();
    }
}
