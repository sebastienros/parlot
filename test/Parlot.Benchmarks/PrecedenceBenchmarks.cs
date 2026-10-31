using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// A C#-like expression grammar of 11 precedence levels (prefix, multiplicative, additive, shift, relational, equality,
/// &amp;, ^, |, &amp;&amp;, ||), parsed level by level (Nested), by the precedence parser a chain of levels is flattened into
/// (Chain), and by an Operators() table (Table), at runtime and generated.
/// </summary>
/// <remarks>
/// The nested chain uses the operator forms whose factories receive the ParseContext, which are the same parsers and are
/// not flattened.
/// </remarks>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public partial class PrecedenceBenchmarks
{
    private Parser<long> _nested = null!;
    private Parser<long> _chain = null!;
    private Parser<long> _table = null!;
    private string _input = "";

    [Params("Atom", "Arithmetic", "Mixed", "Nested")]
    public string Input { get; set; } = "Mixed";

    public static string GetInput(string name) => name switch
    {
        "Atom" => "x",
        "Arithmetic" => "1 + 2 * 3 - 4 / 5 % 6",
        "Mixed" => "a * 2 + b / 3 - c % 4 << 1 < d && e || !f == g & h | i ^ ~j",
        "Nested" => "((1 + 2) * (3 - 4) / -(5 + 6)) << (7 & 8 | 9) >= 10 && -(11) != 12",
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [GlobalSetup]
    public void Setup()
    {
        _nested = PrecedenceGrammar.Create(PrecedenceGrammar.Shape.Nested);
        _chain = PrecedenceGrammar.Create(PrecedenceGrammar.Shape.Chain);
        _table = PrecedenceGrammar.Create(PrecedenceGrammar.Shape.Table);
        _input = GetInput(Input);

        var expected = NestedRuntime();

        if (ChainRuntime() != expected || TableRuntime() != expected
            || NestedGenerated() != expected || ChainGenerated() != expected || TableGenerated() != expected)
        {
            throw new InvalidOperationException("Precedence benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Runtime")]
    public long NestedRuntime() => _nested.Parse(_input);

    [Benchmark, BenchmarkCategory("Runtime")]
    public long ChainRuntime() => _chain.Parse(_input);

    [Benchmark, BenchmarkCategory("Runtime")]
    public long TableRuntime() => _table.Parse(_input);

    [Benchmark(Baseline = true), BenchmarkCategory("Generated")]
    public long NestedGenerated() => TryParseNested(_input, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public long ChainGenerated() => TryParseChain(_input, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public long TableGenerated() => TryParseTable(_input, out var value) ? value : -1;

    public static partial bool TryParseNested(string input, out long value);
    public static partial bool TryParseChain(string input, out long value);
    public static partial bool TryParseTable(string input, out long value);
}

/// <summary>
/// The runtime grammars of <see cref="PrecedenceBenchmarks"/>, built like the generated ones.
/// </summary>
public static class PrecedenceGrammar
{
    public enum Shape
    {
        Nested,
        Chain,
        Table,
    }

    public static Parser<long> Create(Shape shape)
    {
        var expression = Deferred<long>();
        var primary = OneOf(
            Terms.Integer(),
            Terms.Identifier().Then(static x => (long)x.Length),
            Between(Terms.Char('('), expression, Terms.Char(')')));

        if (shape == Shape.Nested)
        {
            Parser<long> nested = primary.Unary(
                (Terms.Text("-"), static (ParseContext _, long x) => -x),
                (Terms.Text("!"), static (ParseContext _, long x) => x == 0 ? 1 : 0),
                (Terms.Text("~"), static (ParseContext _, long x) => ~x));

            foreach (var level in BinaryLevelsWithContext())
            {
                nested = nested.LeftAssociative(level);
            }

            expression.Parser = nested;
            return expression;
        }

        (Parser<string> op, Func<long, long> factory)[] prefix =
        [
            (Terms.Text("-"), static x => -x),
            (Terms.Text("!"), static x => x == 0 ? 1 : 0),
            (Terms.Text("~"), static x => ~x),
        ];

        if (shape == Shape.Table)
        {
            var table = primary.Operators().Unary(prefix);

            foreach (var level in BinaryLevels())
            {
                table = table.LeftAssociative(level);
            }

            expression.Parser = table;
            return expression;
        }

        var chain = primary.Unary(prefix);

        foreach (var level in BinaryLevels())
        {
            chain = chain.LeftAssociative(level);
        }

        expression.Parser = chain;
        return expression;
    }

    private static (Parser<string> op, Func<long, long, long> factory)[][] BinaryLevels() =>
    [
        [(Terms.Text("*"), static (a, b) => a * b), (Terms.Text("/"), static (a, b) => b == 0 ? 0 : a / b), (Terms.Text("%"), static (a, b) => b == 0 ? 0 : a % b)],
        [(Terms.Text("+"), static (a, b) => a + b), (Terms.Text("-"), static (a, b) => a - b)],
        [(Terms.Text("<<"), static (a, b) => a << (int)(b & 63)), (Terms.Text(">>"), static (a, b) => a >> (int)(b & 63))],
        [(Terms.Text("<="), static (a, b) => a <= b ? 1 : 0), (Terms.Text(">="), static (a, b) => a >= b ? 1 : 0), (Terms.Text("<"), static (a, b) => a < b ? 1 : 0), (Terms.Text(">"), static (a, b) => a > b ? 1 : 0)],
        [(Terms.Text("=="), static (a, b) => a == b ? 1 : 0), (Terms.Text("!="), static (a, b) => a != b ? 1 : 0)],
        [(Terms.Text("&"), static (a, b) => a & b)],
        [(Terms.Text("^"), static (a, b) => a ^ b)],
        [(Terms.Text("|"), static (a, b) => a | b)],
        [(Terms.Text("&&"), static (a, b) => a != 0 && b != 0 ? 1 : 0)],
        [(Terms.Text("||"), static (a, b) => a != 0 || b != 0 ? 1 : 0)],
    ];

    private static (Parser<string> op, Func<ParseContext, long, long, long> factory)[][] BinaryLevelsWithContext() =>
    [
        [(Terms.Text("*"), static (_, a, b) => a * b), (Terms.Text("/"), static (_, a, b) => b == 0 ? 0 : a / b), (Terms.Text("%"), static (_, a, b) => b == 0 ? 0 : a % b)],
        [(Terms.Text("+"), static (_, a, b) => a + b), (Terms.Text("-"), static (_, a, b) => a - b)],
        [(Terms.Text("<<"), static (_, a, b) => a << (int)(b & 63)), (Terms.Text(">>"), static (_, a, b) => a >> (int)(b & 63))],
        [(Terms.Text("<="), static (_, a, b) => a <= b ? 1 : 0), (Terms.Text(">="), static (_, a, b) => a >= b ? 1 : 0), (Terms.Text("<"), static (_, a, b) => a < b ? 1 : 0), (Terms.Text(">"), static (_, a, b) => a > b ? 1 : 0)],
        [(Terms.Text("=="), static (_, a, b) => a == b ? 1 : 0), (Terms.Text("!="), static (_, a, b) => a != b ? 1 : 0)],
        [(Terms.Text("&"), static (_, a, b) => a & b)],
        [(Terms.Text("^"), static (_, a, b) => a ^ b)],
        [(Terms.Text("|"), static (_, a, b) => a | b)],
        [(Terms.Text("&&"), static (_, a, b) => a != 0 && b != 0 ? 1 : 0)],
        [(Terms.Text("||"), static (_, a, b) => a != 0 || b != 0 ? 1 : 0)],
    ];
}
