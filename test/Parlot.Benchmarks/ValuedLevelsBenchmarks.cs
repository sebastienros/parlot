using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// A table of the 12 binary levels of JavaScript, whose operators are passed to a single factory per level (Valued), or
/// which have a factory per operator (PerOperator), at runtime and generated. The valued levels used to start a lookup
/// each, which tried one choice per level after every operand.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public partial class ValuedLevelsBenchmarks
{
    private Parser<long> _perOperator = null!;
    private Parser<long> _valued = null!;
    private string _input = "";

    [Params("Atom", "Arguments", "Mixed")]
    public string Input { get; set; } = "Mixed";

    public static string GetInput(string name) => name switch
    {
        "Atom" => "a",
        "Arguments" => "(a, b, 1, (c), d, 2, e, (f, g), 3, h)",
        "Mixed" => "a * 2 + b / 3 - (c % 4) << 1 < d && e == f || g ?? h & 1 | i ^ j ** 2 ** 1 >= k != l >>> 2",
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [GlobalSetup]
    public void Setup()
    {
        _perOperator = ValuedLevelsGrammar.Create(valued: false);
        _valued = ValuedLevelsGrammar.Create(valued: true);
        _input = GetInput(Input);

        var expected = PerOperatorRuntime();

        if (ValuedRuntime() != expected || PerOperatorGenerated() != expected || ValuedGenerated() != expected)
        {
            throw new InvalidOperationException("Valued levels benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Runtime")]
    public long PerOperatorRuntime() => _perOperator.Parse(_input);

    [Benchmark, BenchmarkCategory("Runtime")]
    public long ValuedRuntime() => _valued.Parse(_input);

    [Benchmark(Baseline = true), BenchmarkCategory("Generated")]
    public long PerOperatorGenerated() => TryParsePerOperator(_input, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public long ValuedGenerated() => TryParseValued(_input, out var value) ? value : -1;

    public static partial bool TryParsePerOperator(string input, out long value);
    public static partial bool TryParseValued(string input, out long value);
}

/// <summary>
/// The runtime grammars of <see cref="ValuedLevelsBenchmarks"/>, built like the generated ones.
/// </summary>
public static class ValuedLevelsGrammar
{
    public static Parser<long> Create(bool valued)
    {
        var expression = Deferred<long>();

        // A parenthesized list returns the sum of its expressions, like the arguments of a call
        var primary = OneOf(
            Terms.Integer(),
            Terms.Identifier().Then(static x => (long)x.Length),
            Between(Terms.Char('('), Separated(Terms.Char(','), expression), Terms.Char(')')).Then(static list =>
            {
                long sum = 0;

                foreach (var item in list)
                {
                    sum += item;
                }

                return sum;
            }));

        var table = primary.Operators();
        var levels = Levels();

        for (var i = 0; i < levels.Length; i++)
        {
            var (operators, rightAssociative) = levels[i];

            if (valued)
            {
                var parsers = Array.ConvertAll(operators, static op => (Parser<string>)Terms.Text(op));

                table = rightAssociative
                    ? table.RightAssociative(parsers, static (a, b, op) => Apply(op, a, b))
                    : table.LeftAssociative(parsers, static (a, b, op) => Apply(op, a, b));
            }
            else
            {
                var list = PerOperatorLevels()[i].List;

                table = rightAssociative ? table.RightAssociative(list) : table.LeftAssociative(list);
            }
        }

        expression.Parser = table;
        return expression;
    }

    public static (string[] Operators, bool RightAssociative)[] Levels() =>
    [
        (["**"], true),
        (["*", "/", "%"], false),
        (["+", "-"], false),
        ([">>>", "<<", ">>"], false),
        (["<=", ">=", "<", ">"], false),
        (["===", "!==", "==", "!="], false),
        (["&"], false),
        (["^"], false),
        (["|"], false),
        (["&&"], false),
        (["||"], false),
        (["??"], false),
    ];

    // A factory per operator, which keeps the levels in a single lookup
    public static ((Parser<string> op, Func<long, long, long> factory)[] List, bool RightAssociative)[] PerOperatorLevels() =>
    [
        ([(Terms.Text("**"), static (a, b) => Apply("**", a, b))], true),
        ([(Terms.Text("*"), static (a, b) => Apply("*", a, b)), (Terms.Text("/"), static (a, b) => Apply("/", a, b)), (Terms.Text("%"), static (a, b) => Apply("%", a, b))], false),
        ([(Terms.Text("+"), static (a, b) => Apply("+", a, b)), (Terms.Text("-"), static (a, b) => Apply("-", a, b))], false),
        ([(Terms.Text(">>>"), static (a, b) => Apply(">>>", a, b)), (Terms.Text("<<"), static (a, b) => Apply("<<", a, b)), (Terms.Text(">>"), static (a, b) => Apply(">>", a, b))], false),
        ([(Terms.Text("<="), static (a, b) => Apply("<=", a, b)), (Terms.Text(">="), static (a, b) => Apply(">=", a, b)), (Terms.Text("<"), static (a, b) => Apply("<", a, b)), (Terms.Text(">"), static (a, b) => Apply(">", a, b))], false),
        ([(Terms.Text("==="), static (a, b) => Apply("===", a, b)), (Terms.Text("!=="), static (a, b) => Apply("!==", a, b)), (Terms.Text("=="), static (a, b) => Apply("==", a, b)), (Terms.Text("!="), static (a, b) => Apply("!=", a, b))], false),
        ([(Terms.Text("&"), static (a, b) => Apply("&", a, b))], false),
        ([(Terms.Text("^"), static (a, b) => Apply("^", a, b))], false),
        ([(Terms.Text("|"), static (a, b) => Apply("|", a, b))], false),
        ([(Terms.Text("&&"), static (a, b) => Apply("&&", a, b))], false),
        ([(Terms.Text("||"), static (a, b) => Apply("||", a, b))], false),
        ([(Terms.Text("??"), static (a, b) => Apply("??", a, b))], false),
    ];

    // A value which depends on the shape of the tree and on each operator, such that the parsers are compared on both
    public static long Apply(string op, long a, long b) => unchecked(a * 31 + b * 7 + op switch
    {
        "**" => 1,
        "*" => 2,
        "/" => 3,
        "%" => 4,
        "+" => 5,
        "-" => 6,
        ">>>" => 7,
        "<<" => 8,
        ">>" => 9,
        "<=" => 10,
        ">=" => 11,
        "<" => 12,
        ">" => 13,
        "===" => 14,
        "!==" => 15,
        "==" => 16,
        "!=" => 17,
        "&" => 18,
        "^" => 19,
        "|" => 20,
        "&&" => 21,
        "||" => 22,
        "??" => 23,
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    });
}
