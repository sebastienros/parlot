using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated copies of the grammars of NoNewLineBeforeBenchmarks
public static partial class NoNewLineBeforeGenerated
{
    [GenerateParser(nameof(TryParsePostfixTable))]
    private static Parser<int> BuildPostfixTable()
    {
        var table = Terms.Identifier().Then(static _ => 1).Operators()
            .Postfix((NoNewLineBefore(Terms.Text("++")), static x => x + 1), (NoNewLineBefore(Terms.Text("--")), static x => x + 1))
            .LeftAssociative((Terms.Char('*'), static (a, b) => a + b), (Terms.Char('/'), static (a, b) => a + b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => a + b), (Terms.Char('-'), static (a, b) => a + b));

        return ZeroOrMany(table).Eof().Then(static x => x.Count);
    }

    [GenerateParser(nameof(TryParseArrows))]
    private static Parser<int> BuildArrows()
    {
        var assignment = OneOf(Terms.Text("="), Terms.Text("+=")).And(Terms.Identifier()).Then(static _ => 1);
        var arrow = NoNewLineBefore(Terms.Text("=>")).And(Terms.Identifier()).Then(static _ => 2);
        var statement = Terms.Identifier().And(OneOf(assignment, arrow).Else(0)).Then(static x => x.Item2);
        var arrowStatement = Terms.Text("=>").And(Terms.Identifier()).Then(static _ => 3);

        return ZeroOrMany(OneOf(statement, arrowStatement)).Eof().Then(static x => x.Count);
    }
}
