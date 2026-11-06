using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated grammars of ValuedPrefixBenchmarks, built like ValuedPrefixGrammar
public partial class ValuedPrefixBenchmarks
{
    [GenerateParser(nameof(TryParseOutside))]
    private static Parser<long> BuildOutside()
    {
        var minus = Terms.Char('-').Then(static (_, start, _, _) => start);
        var not = Terms.Char('!').Then(static (_, start, _, _) => start);
        var complement = Terms.Char('~').Then(static (_, start, _, _) => start);
        var typeOf = Terms.Text("typeof").Then(static (_, start, _, _) => start);
        var voidOf = Terms.Text("void").Then(static (_, start, _, _) => start);

        var postfix = Terms.Integer().Operators().Postfix((Terms.Text("++"), static x => x + 1));
        var unary = Deferred<long>();

        unary.Parser = OneOf(
            minus.And(unary).Then(static x => ValuedPrefixGrammar.Prefix(x.Item2, x.Item1, 1)),
            not.And(unary).Then(static x => ValuedPrefixGrammar.Prefix(x.Item2, x.Item1, 2)),
            complement.And(unary).Then(static x => ValuedPrefixGrammar.Prefix(x.Item2, x.Item1, 3)),
            typeOf.And(unary).Then(static x => ValuedPrefixGrammar.Prefix(x.Item2, x.Item1, 4)),
            voidOf.And(unary).Then(static x => ValuedPrefixGrammar.Prefix(x.Item2, x.Item1, 5)),
            postfix);

        return unary.Operators()
            .LeftAssociative((Terms.Char('*'), static (a, b) => (a * 3) + b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => (a * 5) + b), (Terms.Char('-'), static (a, b) => (a * 5) - b));
    }

    [GenerateParser(nameof(TryParseTable))]
    private static Parser<long> BuildTable()
    {
        var minus = Terms.Char('-').Then(static (_, start, _, _) => start);
        var not = Terms.Char('!').Then(static (_, start, _, _) => start);
        var complement = Terms.Char('~').Then(static (_, start, _, _) => start);
        var typeOf = Terms.Text("typeof").Then(static (_, start, _, _) => start);
        var voidOf = Terms.Text("void").Then(static (_, start, _, _) => start);

        return Terms.Integer().Operators()
            .Postfix((Terms.Text("++"), static x => x + 1))
            .Unary(
                (minus, static (x, start) => ValuedPrefixGrammar.Prefix(x, start, 1)),
                (not, static (x, start) => ValuedPrefixGrammar.Prefix(x, start, 2)),
                (complement, static (x, start) => ValuedPrefixGrammar.Prefix(x, start, 3)),
                (typeOf, static (x, start) => ValuedPrefixGrammar.Prefix(x, start, 4)),
                (voidOf, static (x, start) => ValuedPrefixGrammar.Prefix(x, start, 5)))
            .LeftAssociative((Terms.Char('*'), static (a, b) => (a * 3) + b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => (a * 5) + b), (Terms.Char('-'), static (a, b) => (a * 5) - b));
    }
}
