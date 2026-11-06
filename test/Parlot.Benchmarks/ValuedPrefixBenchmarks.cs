using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using System.Text;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// Prefix operators whose nodes need the position of the operator, like the ones of a JavaScript parser, at runtime and
/// generated. Before valued prefix levels, they were parsed outside the table, by a recursive choice of the operators and
/// of the table of the tighter levels (Outside). They are now a level of the table whose factory receives the position
/// (Table). Plain is the table with a prefix level whose factories don't receive the positions, which parses the same
/// text, the cost of the prefix levels of a table without values.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public partial class ValuedPrefixBenchmarks
{
    private Parser<long> _outside = null!;
    private Parser<long> _table = null!;
    private string _input = "";

    [Params("Operands", "Prefixes")]
    public string Input { get; set; } = "Prefixes";

    public static string GetInput(string name)
    {
        var random = new Random(516);
        var sb = new StringBuilder();
        string[] prefixes = ["-", "!", "~", "typeof ", "void "];
        string[] binaries = [" + ", " * ", " - "];

        for (var i = 0; i < 200; i++)
        {
            if (i > 0)
            {
                sb.Append(binaries[random.Next(binaries.Length)]);
            }

            // Mostly no prefix operator, then mostly one
            var count = name switch
            {
                "Operands" => 0,
                "Prefixes" => random.Next(4) switch { 0 => 0, 1 or 2 => 1, _ => 2 },
                _ => throw new ArgumentOutOfRangeException(nameof(name)),
            };

            for (var j = 0; j < count; j++)
            {
                sb.Append(prefixes[random.Next(prefixes.Length)]);
            }

            sb.Append(random.Next(1000));

            if (random.Next(8) == 0)
            {
                sb.Append("++");
            }
        }

        return sb.ToString();
    }

    [GlobalSetup]
    public void Setup()
    {
        _outside = ValuedPrefixGrammar.Create(table: false);
        _table = ValuedPrefixGrammar.Create(table: true);
        _input = GetInput(Input);

        var expected = OutsideRuntime();

        if (TableRuntime() != expected || OutsideGenerated() != expected || TableGenerated() != expected)
        {
            throw new InvalidOperationException("Valued prefix benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Runtime")]
    public long OutsideRuntime() => _outside.Parse(_input);

    [Benchmark, BenchmarkCategory("Runtime")]
    public long TableRuntime() => _table.Parse(_input);

    [Benchmark(Baseline = true), BenchmarkCategory("Generated")]
    public long OutsideGenerated() => TryParseOutside(_input, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public long TableGenerated() => TryParseTable(_input, out var value) ? value : -1;

    private Parser<long> _plain = ValuedPrefixGrammar.CreatePlain();

    [Benchmark, BenchmarkCategory("Runtime")]
    public long PlainRuntime() => _plain.Parse(_input);

    [Benchmark, BenchmarkCategory("Generated")]
    public long PlainGenerated() => TryParsePlain(_input, out var value) ? value : -1;

    public static partial bool TryParsePlain(string input, out long value);
    public static partial bool TryParseOutside(string input, out long value);
    public static partial bool TryParseTable(string input, out long value);
}

/// <summary>
/// The runtime grammars of <see cref="ValuedPrefixBenchmarks"/>, built like the generated ones. The values combine the
/// operators, the operands and the positions of the prefix operators.
/// </summary>
public static class ValuedPrefixGrammar
{
    public static long Prefix(long operand, int position, long op) => (operand * 31) + (position * 7) + op;

    // The positions are not passed, the values differ from the ones of Create()
    public static Parser<long> CreatePlain() => Terms.Integer().Operators()
            .Postfix((Terms.Text("++"), static x => x + 1))
            .Unary(
                (Terms.Text("-"), static x => Prefix(x, 0, 1)),
                (Terms.Text("!"), static x => Prefix(x, 0, 2)),
                (Terms.Text("~"), static x => Prefix(x, 0, 3)),
                (Terms.Text("typeof"), static x => Prefix(x, 0, 4)),
                (Terms.Text("void"), static x => Prefix(x, 0, 5)))
            .LeftAssociative((Terms.Char('*'), static (a, b) => (a * 3) + b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => (a * 5) + b), (Terms.Char('-'), static (a, b) => (a * 5) - b));

    public static Parser<long> Create(bool table)
    {
        var minus = Terms.Char('-').Then(static (_, start, _, _) => start);
        var not = Terms.Char('!').Then(static (_, start, _, _) => start);
        var complement = Terms.Char('~').Then(static (_, start, _, _) => start);
        var typeOf = Terms.Text("typeof").Then(static (_, start, _, _) => start);
        var voidOf = Terms.Text("void").Then(static (_, start, _, _) => start);

        var postfix = Terms.Integer().Operators().Postfix((Terms.Text("++"), static x => x + 1));

        if (table)
        {
            return postfix
                .Unary(
                    (minus, static (x, start) => Prefix(x, start, 1)),
                    (not, static (x, start) => Prefix(x, start, 2)),
                    (complement, static (x, start) => Prefix(x, start, 3)),
                    (typeOf, static (x, start) => Prefix(x, start, 4)),
                    (voidOf, static (x, start) => Prefix(x, start, 5)))
                .LeftAssociative((Terms.Char('*'), static (a, b) => (a * 3) + b))
                .LeftAssociative((Terms.Char('+'), static (a, b) => (a * 5) + b), (Terms.Char('-'), static (a, b) => (a * 5) - b));
        }

        var unary = Deferred<long>();

        unary.Parser = OneOf(
            minus.And(unary).Then(static x => Prefix(x.Item2, x.Item1, 1)),
            not.And(unary).Then(static x => Prefix(x.Item2, x.Item1, 2)),
            complement.And(unary).Then(static x => Prefix(x.Item2, x.Item1, 3)),
            typeOf.And(unary).Then(static x => Prefix(x.Item2, x.Item1, 4)),
            voidOf.And(unary).Then(static x => Prefix(x.Item2, x.Item1, 5)),
            postfix);

        return unary.Operators()
            .LeftAssociative((Terms.Char('*'), static (a, b) => (a * 3) + b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => (a * 5) + b), (Terms.Char('-'), static (a, b) => (a * 5) - b));
    }
}
