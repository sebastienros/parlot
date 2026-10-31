#nullable enable

using System;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Chains of LeftAssociative, RightAssociative and Unary parsers, which are generated as a single precedence parser, and
// the same chains with each level separated from its operand, which are generated level by level. Each grammar returns
// the expression and the text it didn't read, and is compared with the runtime in FlattenedChainTests.
public static partial class FlattenedChainGrammars
{
    [GenerateParser(nameof(TryParseChain))]
    private static Parser<string> BuildChain() => Chain(nested: false, prefixAboveExponent: false);

    [GenerateParser(nameof(TryParseNested))]
    private static Parser<string> BuildNested() => Chain(nested: true, prefixAboveExponent: false);

    [GenerateParser(nameof(TryParseChainPrefixAboveExponent))]
    private static Parser<string> BuildChainPrefixAboveExponent() => Chain(nested: false, prefixAboveExponent: true);

    [GenerateParser(nameof(TryParseNestedPrefixAboveExponent))]
    private static Parser<string> BuildNestedPrefixAboveExponent() => Chain(nested: true, prefixAboveExponent: true);

    private static Parser<string> WithRest(Parser<string> parser) =>
        parser.And(Literals.Pattern(static _ => true, minSize: 0)).Then(static x => x.Item1 + " | " + x.Item2.ToString());

    private static Parser<string> Level(Parser<string> parser, bool nested) => nested ? parser.Then(static x => x) : parser;

    // A C#-like table where the operators of tighter levels are often the start of operators of looser ones
    private static Parser<string> Chain(bool nested, bool prefixAboveExponent)
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        (Parser<string> op, Func<string, string> factory)[] prefix =
        [
            (Terms.Text("-"), static x => PrecedenceAst.Prefix("-", x)),
            (Terms.Text("!"), static x => PrecedenceAst.Prefix("!", x)),
            (Terms.Text("~"), static x => PrecedenceAst.Prefix("~", x)),
        ];

        (Parser<string> op, Func<string, string, string> factory)[] exponent = [(Terms.Text("**"), static (a, b) => PrecedenceAst.Binary("**", a, b))];

        Parser<string> chain = prefixAboveExponent
            ? Level(Level(primary.RightAssociative(exponent), nested).Unary(prefix), nested)
            : Level(Level(primary.Unary(prefix), nested).RightAssociative(exponent), nested);

        chain = Level(chain.LeftAssociative(
            (Terms.Text("*"), static (a, b) => PrecedenceAst.Binary("*", a, b)),
            (Terms.Text("/"), static (a, b) => PrecedenceAst.Binary("/", a, b)),
            (Terms.Text("%"), static (a, b) => PrecedenceAst.Binary("%", a, b))), nested);
        chain = Level(chain.LeftAssociative(
            (Terms.Text("+"), static (a, b) => PrecedenceAst.Binary("+", a, b)),
            (Terms.Text("-"), static (a, b) => PrecedenceAst.Binary("-", a, b))), nested);
        chain = Level(chain.LeftAssociative(
            (Terms.Text("<<"), static (a, b) => PrecedenceAst.Binary("<<", a, b)),
            (Terms.Text(">>"), static (a, b) => PrecedenceAst.Binary(">>", a, b))), nested);
        chain = Level(chain.LeftAssociative(
            (Terms.Text("<"), static (a, b) => PrecedenceAst.Binary("<", a, b)),
            (Terms.Text(">"), static (a, b) => PrecedenceAst.Binary(">", a, b)),
            (Terms.Text("<="), static (a, b) => PrecedenceAst.Binary("<=", a, b)),
            (Terms.Text(">="), static (a, b) => PrecedenceAst.Binary(">=", a, b))), nested);
        chain = Level(chain.LeftAssociative(
            (Terms.Text("=="), static (a, b) => PrecedenceAst.Binary("==", a, b)),
            (Terms.Text("!="), static (a, b) => PrecedenceAst.Binary("!=", a, b)),
            (Terms.Text("="), static (a, b) => PrecedenceAst.Binary("=", a, b))), nested);
        chain = Level(chain.LeftAssociative((Terms.Text("&"), static (a, b) => PrecedenceAst.Binary("&", a, b))), nested);
        chain = Level(chain.LeftAssociative((Terms.Text("^"), static (a, b) => PrecedenceAst.Binary("^", a, b))), nested);
        chain = Level(chain.LeftAssociative((Terms.Text("|"), static (a, b) => PrecedenceAst.Binary("|", a, b))), nested);
        chain = Level(chain.LeftAssociative((Terms.Text("&&"), static (a, b) => PrecedenceAst.Binary("&&", a, b))), nested);
        chain = Level(chain.LeftAssociative((Terms.Text("||"), static (a, b) => PrecedenceAst.Binary("||", a, b))), nested);

        expression.Parser = chain.RightAssociative(
            (Terms.Text("<="), static (a, b) => PrecedenceAst.Binary("<=", a, b)),
            (Terms.Text("&="), static (a, b) => PrecedenceAst.Binary("&=", a, b)),
            (Terms.Text("|="), static (a, b) => PrecedenceAst.Binary("|=", a, b)),
            (Terms.Text("=>"), static (a, b) => PrecedenceAst.Binary("=>", a, b)));

        return WithRest(expression);
    }
}
