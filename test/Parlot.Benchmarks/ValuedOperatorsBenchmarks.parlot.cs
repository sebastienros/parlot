using System;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated grammars of ValuedOperatorsBenchmarks, built like ValuedOperatorsGrammar
public partial class ValuedOperatorsBenchmarks
{
    [GenerateParser(nameof(TryParseFold))]
    private static Parser<long> BuildFold()
    {
        var expression = Deferred<long>();

        // The postfix operators are folded after the primary, and the conditional is a level of its own
        var postfix = OneOf(
            Terms.Char('.').SkipAnd(Terms.Identifier()).Then(static name => (false, (long)name.Length)),
            Between(Terms.Char('['), expression, Terms.Char(']')).Then(static i => (true, i)));

        var operand = Primary(expression).And(ZeroOrMany(postfix)).Then(static x =>
        {
            var value = x.Item1;

            foreach (var (isIndex, v) in x.Item2)
            {
                value = isIndex ? value * 37 + v : value * 31 + v;
            }

            return value;
        });

        var binary = operand.Operators().Unary(Prefix());

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

    [GenerateParser(nameof(TryParseTable))]
    private static Parser<long> BuildTable()
    {
        var expression = Deferred<long>();

        // The member accesses, the indexers and the conditional are levels of the table
        var table = Primary(expression).Operators()
            .Postfix(
                (Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => x * 31 + name.Length),
                (Between(Terms.Char('['), expression, Terms.Char(']')), static (x, i) => x * 37 + i))
            .Unary(Prefix());

        foreach (var level in BinaryLevels())
        {
            table = table.LeftAssociative(level);
        }

        expression.Parser = table
            .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (c, a, b) => c != 0 ? a : b, rightAssociative: false)
            .LeftAssociative((Terms.Text("??"), static (a, b) => a != 0 ? a : b));

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
    ];

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
