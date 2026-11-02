#nullable enable

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using System.Text;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// An Operators() table of twelve levels which builds a tree of nodes, like NCalc's: a postfix level, a right-associative
/// level, prefix operators, then binary levels, at runtime and generated. Simple and Calls have no prefix operator,
/// Prefixes has single ones and Chains has chains of them.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public partial class ExpressionTableBenchmarks
{
    private Parser<ExpressionNode> _parser = null!;
    private string _input = "";

    [Params("Simple", "Calls", "Prefixes", "Chains")]
    public string Input { get; set; } = "Calls";

    public static string GetInput(string name) => name switch
    {
        "Simple" => "(3 < level && 5 >= level)",
        "Calls" => "state == 4 && a == 1 && f() == 1 || g(1 + 1, 1, 2, 3) && name == 4",
        "Prefixes" => "-a * b + !c == ~d && -(e - -f) || !g ?? h' ** 2",
        "Chains" => "- - a + !!b * ~-c ** 2 == !~-d && - - -e",
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [GlobalSetup]
    public void Setup()
    {
        _parser = ExpressionTableGrammar.Create();
        _input = GetInput(Input);

        if (Generated()?.ToString() != Runtime().ToString())
        {
            throw new InvalidOperationException("Expression table benchmark returned an unexpected result.");
        }
    }

    [Benchmark, BenchmarkCategory("Runtime")]
    public ExpressionNode Runtime() => _parser.Parse(_input)!;

    [Benchmark, BenchmarkCategory("Generated")]
    public ExpressionNode? Generated() => TryParse(_input, out var value) ? value : null;

    public static partial bool TryParse(string input, out ExpressionNode value);
}

/// <summary>
/// A node of the trees <see cref="ExpressionTableBenchmarks"/> builds: an operator and its operands, or a leaf.
/// </summary>
public sealed class ExpressionNode
{
    public ExpressionNode(string kind, ExpressionNode? left = null, ExpressionNode? right = null, long value = 0)
    {
        Kind = kind;
        Left = left;
        Right = right;
        Value = value;
    }

    public string Kind { get; }
    public ExpressionNode? Left { get; }
    public ExpressionNode? Right { get; }
    public long Value { get; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        Append(builder);
        return builder.ToString();
    }

    private void Append(StringBuilder builder)
    {
        builder.Append('(').Append(Kind);

        if (Left is null)
        {
            builder.Append(' ').Append(Value);
        }

        Left?.Append(builder.Append(' '));
        Right?.Append(builder.Append(' '));
        builder.Append(')');
    }
}

/// <summary>
/// The runtime grammar of <see cref="ExpressionTableBenchmarks"/>, built like the generated one.
/// </summary>
public static class ExpressionTableGrammar
{
    public static Parser<ExpressionNode> Create()
    {
        var expression = Deferred<ExpressionNode>();
        // The number of arguments of a call, which can have none
        var arguments = Between(Terms.Char('('), Separated(Terms.Char(','), expression).Optional(), Terms.Char(')'))
            .Then(static x => x.HasValue ? x.Value.Count : 0);
        var primary = OneOf(
            Terms.Integer().Then(static x => new ExpressionNode("number", value: x)),
            Terms.Identifier().And(arguments.Optional()).Then(static x => x.Item2.HasValue
                ? new ExpressionNode("call", value: x.Item1.Length + x.Item2.Value)
                : new ExpressionNode("name", value: x.Item1.Length)),
            Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix((Terms.Char('\''), static x => new ExpressionNode("'", x)))
            .RightAssociative((Terms.Text("**"), static (a, b) => new ExpressionNode("**", a, b)))
            .Unary(
                (Terms.Text("!"), static x => new ExpressionNode("!", x)),
                (Terms.Text("-"), static x => new ExpressionNode("neg", x)),
                (Terms.Text("~"), static x => new ExpressionNode("~", x)))
            .LeftAssociative(
                (Terms.Text("*"), static (a, b) => new ExpressionNode("*", a, b)),
                (Terms.Text("/"), static (a, b) => new ExpressionNode("/", a, b)),
                (Terms.Text("%"), static (a, b) => new ExpressionNode("%", a, b)))
            .LeftAssociative(
                (Terms.Text("+"), static (a, b) => new ExpressionNode("+", a, b)),
                (Terms.Text("-"), static (a, b) => new ExpressionNode("-", a, b)))
            .LeftAssociative(
                (Terms.Text("<<"), static (a, b) => new ExpressionNode("<<", a, b)),
                (Terms.Text(">>"), static (a, b) => new ExpressionNode(">>", a, b)))
            .LeftAssociative(
                (Terms.Text("<="), static (a, b) => new ExpressionNode("<=", a, b)),
                (Terms.Text(">="), static (a, b) => new ExpressionNode(">=", a, b)),
                (Terms.Text("<"), static (a, b) => new ExpressionNode("<", a, b)),
                (Terms.Text(">"), static (a, b) => new ExpressionNode(">", a, b)))
            .LeftAssociative(
                (Terms.Text("=="), static (a, b) => new ExpressionNode("==", a, b)),
                (Terms.Text("!="), static (a, b) => new ExpressionNode("!=", a, b)))
            .LeftAssociative((Terms.Text("^"), static (a, b) => new ExpressionNode("^", a, b)))
            .LeftAssociative((Terms.Text("&&"), static (a, b) => new ExpressionNode("&&", a, b)))
            .LeftAssociative((Terms.Text("||"), static (a, b) => new ExpressionNode("||", a, b)))
            .RightAssociative((Terms.Text("??"), static (a, b) => new ExpressionNode("??", a, b)));

        return expression.Eof();
    }
}
