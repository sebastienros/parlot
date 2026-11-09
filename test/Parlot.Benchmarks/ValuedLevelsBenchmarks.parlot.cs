using System;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated grammars of ValuedLevelsBenchmarks, built like ValuedLevelsGrammar
public partial class ValuedLevelsBenchmarks
{
    [GenerateParser(nameof(TryParsePerOperator))]
    private static Parser<long> BuildPerOperator() => Build(valued: false);

    [GenerateParser(nameof(TryParseValued))]
    private static Parser<long> BuildValued() => Build(valued: true);

    private static Parser<long> Build(bool valued)
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
        var levels = ValuedLevelsGrammar.Levels();

        for (var i = 0; i < levels.Length; i++)
        {
            var (operators, rightAssociative) = levels[i];

            if (valued)
            {
                var parsers = Array.ConvertAll(operators, static op => (Parser<string>)Terms.Text(op));

                table = rightAssociative
                    ? table.RightAssociative(parsers, static (a, b, op) => ValuedLevelsGrammar.Apply(op, a, b))
                    : table.LeftAssociative(parsers, static (a, b, op) => ValuedLevelsGrammar.Apply(op, a, b));
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

    // A factory per operator, which keeps the levels in a single lookup
    public static ((Parser<string> op, Func<long, long, long> factory)[] List, bool RightAssociative)[] PerOperatorLevels() =>
    [
        ([(Terms.Text("**"), static (a, b) => ValuedLevelsGrammar.Apply("**", a, b))], true),
        ([(Terms.Text("*"), static (a, b) => ValuedLevelsGrammar.Apply("*", a, b)), (Terms.Text("/"), static (a, b) => ValuedLevelsGrammar.Apply("/", a, b)), (Terms.Text("%"), static (a, b) => ValuedLevelsGrammar.Apply("%", a, b))], false),
        ([(Terms.Text("+"), static (a, b) => ValuedLevelsGrammar.Apply("+", a, b)), (Terms.Text("-"), static (a, b) => ValuedLevelsGrammar.Apply("-", a, b))], false),
        ([(Terms.Text(">>>"), static (a, b) => ValuedLevelsGrammar.Apply(">>>", a, b)), (Terms.Text("<<"), static (a, b) => ValuedLevelsGrammar.Apply("<<", a, b)), (Terms.Text(">>"), static (a, b) => ValuedLevelsGrammar.Apply(">>", a, b))], false),
        ([(Terms.Text("<="), static (a, b) => ValuedLevelsGrammar.Apply("<=", a, b)), (Terms.Text(">="), static (a, b) => ValuedLevelsGrammar.Apply(">=", a, b)), (Terms.Text("<"), static (a, b) => ValuedLevelsGrammar.Apply("<", a, b)), (Terms.Text(">"), static (a, b) => ValuedLevelsGrammar.Apply(">", a, b))], false),
        ([(Terms.Text("==="), static (a, b) => ValuedLevelsGrammar.Apply("===", a, b)), (Terms.Text("!=="), static (a, b) => ValuedLevelsGrammar.Apply("!==", a, b)), (Terms.Text("=="), static (a, b) => ValuedLevelsGrammar.Apply("==", a, b)), (Terms.Text("!="), static (a, b) => ValuedLevelsGrammar.Apply("!=", a, b))], false),
        ([(Terms.Text("&"), static (a, b) => ValuedLevelsGrammar.Apply("&", a, b))], false),
        ([(Terms.Text("^"), static (a, b) => ValuedLevelsGrammar.Apply("^", a, b))], false),
        ([(Terms.Text("|"), static (a, b) => ValuedLevelsGrammar.Apply("|", a, b))], false),
        ([(Terms.Text("&&"), static (a, b) => ValuedLevelsGrammar.Apply("&&", a, b))], false),
        ([(Terms.Text("||"), static (a, b) => ValuedLevelsGrammar.Apply("||", a, b))], false),
        ([(Terms.Text("??"), static (a, b) => ValuedLevelsGrammar.Apply("??", a, b))], false),
    ];
}
