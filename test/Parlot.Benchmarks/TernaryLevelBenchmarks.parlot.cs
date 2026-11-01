using System;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated grammars of TernaryLevelBenchmarks, built like TernaryLevelGrammar
public partial class TernaryLevelBenchmarks
{
    [GenerateParser(nameof(TryParseSequence))]
    private static Parser<long> BuildSequence() => Create(level: false);

    [GenerateParser(nameof(TryParseLevel))]
    private static Parser<long> BuildLevel() => Create(level: true);

    private static Parser<long> Create(bool level)
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
