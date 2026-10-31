using System;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated grammars of PrecedenceBenchmarks, built like PrecedenceGrammar
public partial class PrecedenceBenchmarks
{
    [GenerateParser(nameof(TryParseNested))]
    private static Parser<long> BuildNested()
    {
        var expression = Deferred<long>();

        Parser<long> nested = Primary(expression).Unary(
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

    [GenerateParser(nameof(TryParseChain))]
    private static Parser<long> BuildChain()
    {
        var expression = Deferred<long>();
        var chain = Primary(expression).Unary(Prefix());

        foreach (var level in BinaryLevels())
        {
            chain = chain.LeftAssociative(level);
        }

        expression.Parser = chain;
        return expression;
    }

    [GenerateParser(nameof(TryParseTable))]
    private static Parser<long> BuildTable()
    {
        var expression = Deferred<long>();
        var table = Primary(expression).Operators().Unary(Prefix());

        foreach (var level in BinaryLevels())
        {
            table = table.LeftAssociative(level);
        }

        expression.Parser = table;
        return expression;
    }

    private static Parser<long> Primary(Deferred<long> expression) => OneOf(
        Terms.Integer(),
        Terms.Identifier().Then(static x => (long)x.Length),
        Between(Terms.Char('('), expression, Terms.Char(')')));

    private static (Parser<string> op, Func<long, long> factory)[] Prefix() =>
    [
        (Terms.Text("-"), static x => -x),
        (Terms.Text("!"), static x => x == 0 ? 1 : 0),
        (Terms.Text("~"), static x => ~x),
    ];

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
