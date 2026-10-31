#nullable enable

using System;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Operators() tables. Each grammar returns the expression and the text it didn't read, and is compared with the runtime
// in PrecedenceTests.
public static partial class PrecedenceGrammars
{
    [GenerateParser(nameof(TryParseTable))]
    private static Parser<string> BuildTable()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix((Terms.Char('!'), static x => PrecedenceAst.Suffix("!", x)))
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)), (Terms.Char('~'), static x => PrecedenceAst.Prefix("~", x)))
            .RightAssociative((Terms.Text("**"), static (a, b) => PrecedenceAst.Binary("**", a, b)))
            .LeftAssociative((Terms.Char('*'), static (a, b) => PrecedenceAst.Binary("*", a, b)), (Terms.Char('/'), static (a, b) => PrecedenceAst.Binary("/", a, b)))
            .LeftAssociative((Terms.Char('+'), static (a, b) => PrecedenceAst.Binary("+", a, b)), (Terms.Char('-'), static (a, b) => PrecedenceAst.Binary("-", a, b)))
            .Postfix((Terms.Char('?'), static x => PrecedenceAst.Suffix("?", x)))
            .LeftAssociative((Terms.Text("<<"), static (a, b) => PrecedenceAst.Binary("<<", a, b)))
            .LeftAssociative((Terms.Text("<"), static (a, b) => PrecedenceAst.Binary("<", a, b)), (Terms.Text("<="), static (a, b) => PrecedenceAst.Binary("<=", a, b)))
            .Unary((Terms.Text("not"), static x => PrecedenceAst.Prefix("not ", x)))
            .RightAssociative((Terms.Text("<=="), static (a, b) => PrecedenceAst.Binary("<==", a, b)), (Terms.Text("="), static (a, b) => PrecedenceAst.Binary("=", a, b)));

        return WithRest(expression);
    }

    [GenerateParser(nameof(TryParseZeroWidthPrefix))]
    private static Parser<string> BuildZeroWidthPrefix() =>
        WithRest(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)).Operators()
            .Unary(
                (ZeroOrMany(Literals.Char('-')).Then(static _ => 0), static x => PrecedenceAst.Prefix("-", x)),
                (Literals.Char('+').Then(static _ => 0), static x => PrecedenceAst.Prefix("+", x)))
            .LeftAssociative((Literals.Char('*').Then(static _ => 0), static (a, b) => PrecedenceAst.Binary("*", a, b))));

    [GenerateParser(nameof(TryParseEmptyOperators))]
    private static Parser<string> BuildEmptyOperators() =>
        WithRest(Literals.Text("x").Optional().Then(static x => x.HasValue ? "x" : "").Operators()
            .Postfix((Literals.Text("!").Optional(), static x => PrecedenceAst.Suffix("!", x)))
            .LeftAssociative((Literals.Text("+").Optional(), static (a, b) => PrecedenceAst.Binary("+", a, b)))
            .RightAssociative((Literals.Text("^").Optional(), static (a, b) => PrecedenceAst.Binary("^", a, b))));

    private static Parser<string> WithRest(Parser<string> parser) =>
        parser.And(Literals.Pattern(static _ => true, minSize: 0)).Then(static x => x.Item1 + " | " + x.Item2.ToString());
}
