using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// An expression grammar with member accesses, indexers and a conditional, like the Cosmos DB SQL scalar expressions.
/// The postfix operators are folded with ZeroOrMany after the primary and the conditional is a LeftAssociative level
/// whose operator is <c>? expression :</c> (Fold), or all are levels of an Operators() table (Table), at runtime and generated.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public partial class ValuedOperatorsBenchmarks
{
    private Parser<long> _fold = null!;
    private Parser<long> _table = null!;
    private string _input = "";

    [Params("Atom", "Members", "Conditional", "Mixed")]
    public string Input { get; set; } = "Mixed";

    public static string GetInput(string name) => name switch
    {
        "Atom" => "c",
        "Members" => "c.name.first[2].size + c.items[1 + 2] * 3",
        "Conditional" => "c.a < 2 ? c.b[1] : c.d.e ?? 7",
        "Mixed" => "-(c.a[2] * 3) + c.b.c >= 4 && c.d[c.e.f] != 5 ? c.g : c.h[1] ?? c.i.j",
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [GlobalSetup]
    public void Setup()
    {
        _fold = ValuedOperatorsGrammar.Create(operatorTable: false);
        _table = ValuedOperatorsGrammar.Create(operatorTable: true);
        _input = GetInput(Input);

        var expected = FoldRuntime();

        if (TableRuntime() != expected || FoldGenerated() != expected || TableGenerated() != expected)
        {
            throw new InvalidOperationException("Valued operators benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Runtime")]
    public long FoldRuntime() => _fold.Parse(_input);

    [Benchmark, BenchmarkCategory("Runtime")]
    public long TableRuntime() => _table.Parse(_input);

    [Benchmark(Baseline = true), BenchmarkCategory("Generated")]
    public long FoldGenerated() => TryParseFold(_input, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public long TableGenerated() => TryParseTable(_input, out var value) ? value : -1;

    public static partial bool TryParseFold(string input, out long value);
    public static partial bool TryParseTable(string input, out long value);
}

/// <summary>
/// The runtime grammars of <see cref="ValuedOperatorsBenchmarks"/>, built like the generated ones.
/// </summary>
public static class ValuedOperatorsGrammar
{
    public static Parser<long> Create(bool operatorTable)
    {
        var expression = Deferred<long>();
        var primary = OneOf(
            Terms.Integer(),
            Terms.Identifier().Then(static x => (long)x.Length),
            Between(Terms.Char('('), expression, Terms.Char(')')));
        var member = Terms.Char('.').SkipAnd(Terms.Identifier());
        var index = Between(Terms.Char('['), expression, Terms.Char(']'));
        (Parser<string> op, Func<long, long> factory)[] prefix =
        [
            (Terms.Text("-"), static x => -x),
            (Terms.Text("!"), static x => x == 0 ? 1 : 0),
        ];

        if (operatorTable)
        {
            // The member accesses, the indexers and the conditional are levels of the table
            var table = primary.Operators()
                .Postfix((member, static (x, name) => x * 31 + name.Length), (index, static (x, i) => x * 37 + i))
                .Unary(prefix);

            foreach (var level in BinaryLevels())
            {
                table = table.LeftAssociative(level);
            }

            expression.Parser = table
                .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (c, a, b) => c != 0 ? a : b, rightAssociative: false)
                .LeftAssociative((Terms.Text("??"), static (a, b) => a != 0 ? a : b));

            return expression;
        }

        // The postfix operators are folded after the primary, and the conditional is a level of its own
        var postfix = OneOf(
            member.Then(static name => (false, (long)name.Length)),
            index.Then(static i => (true, i)));

        var operand = primary.And(ZeroOrMany(postfix)).Then(static x =>
        {
            var value = x.Item1;

            foreach (var (isIndex, v) in x.Item2)
            {
                value = isIndex ? value * 37 + v : value * 31 + v;
            }

            return value;
        });

        var binary = operand.Operators().Unary(prefix);

        foreach (var level in BinaryLevels())
        {
            binary = binary.LeftAssociative(level);
        }

        var conditional = ((Parser<long>)binary).LeftAssociative(
            new[] { Terms.Char('?').SkipAnd(expression).AndSkip(Terms.Char(':')) },
            static (c, b, a) => c != 0 ? a : b);

        expression.Parser = conditional.LeftAssociative((Terms.Text("??"), static (a, b) => a != 0 ? a : b));
        return expression;
    }

    private static (Parser<string> op, Func<long, long, long> factory)[][] BinaryLevels() =>
    [
        [(Terms.Text("*"), static (a, b) => a * b), (Terms.Text("/"), static (a, b) => b == 0 ? 0 : a / b)],
        [(Terms.Text("+"), static (a, b) => a + b), (Terms.Text("-"), static (a, b) => a - b)],
        [(Terms.Text("<="), static (a, b) => a <= b ? 1 : 0), (Terms.Text(">="), static (a, b) => a >= b ? 1 : 0), (Terms.Text("<"), static (a, b) => a < b ? 1 : 0), (Terms.Text(">"), static (a, b) => a > b ? 1 : 0)],
        [(Terms.Text("=="), static (a, b) => a == b ? 1 : 0), (Terms.Text("!="), static (a, b) => a != b ? 1 : 0)],
        [(Terms.Text("&&"), static (a, b) => a != 0 && b != 0 ? 1 : 0)],
        [(Terms.Text("||"), static (a, b) => a != 0 || b != 0 ? 1 : 0)],
    ];
}
