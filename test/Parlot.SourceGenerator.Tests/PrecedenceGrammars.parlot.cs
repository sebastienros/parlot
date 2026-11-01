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

    // Valued postfix and binary levels and a conditional, the same table as PrecedenceTests.RuntimeValued
    [GenerateParser(nameof(TryParseValued))]
    private static Parser<string> BuildValued()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix(
                (Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => PrecedenceAst.Member(x, name.ToString())),
                (Between(Terms.Char('['), expression, Terms.Char(']')), static (x, index) => PrecedenceAst.Index(x, index)))
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)), (Terms.Char('~'), static x => PrecedenceAst.Prefix("~", x)))
            .Postfix((Terms.Char('!'), static (x, op) => PrecedenceAst.Suffix(op.ToString(), x)), (Terms.Char('#'), static (x, op) => PrecedenceAst.Suffix(op + "" + op, x)))
            .RightAssociative(new[] { Terms.Text("**") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative(new[] { Terms.Text("*"), Terms.Text("/") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => PrecedenceAst.Binary("+", a, b)), (Terms.Char('-'), static (a, b) => PrecedenceAst.Binary("-", a, b)))
            .LeftAssociative(new[] { Terms.Text("<="), Terms.Text("<") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (c, a, b) => PrecedenceAst.Conditional(c, a, b), rightAssociative: false)
            .LeftAssociative((Terms.Text("??"), static (a, b) => PrecedenceAst.Binary("??", a, b)))
            .RightAssociative(new[] { Terms.Text("="), Terms.Text("+=") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b));

        return WithRest(expression);
    }

    [GenerateParser(nameof(TryParseValuedRight))]
    private static Parser<string> BuildValuedRight()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix(
                (Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => PrecedenceAst.Member(x, name.ToString())),
                (Between(Terms.Char('['), expression, Terms.Char(']')), static (x, index) => PrecedenceAst.Index(x, index)))
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)), (Terms.Char('~'), static x => PrecedenceAst.Prefix("~", x)))
            .Postfix((Terms.Char('!'), static (x, op) => PrecedenceAst.Suffix(op.ToString(), x)), (Terms.Char('#'), static (x, op) => PrecedenceAst.Suffix(op + "" + op, x)))
            .RightAssociative(new[] { Terms.Text("**") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative(new[] { Terms.Text("*"), Terms.Text("/") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => PrecedenceAst.Binary("+", a, b)), (Terms.Char('-'), static (a, b) => PrecedenceAst.Binary("-", a, b)))
            .LeftAssociative(new[] { Terms.Text("<="), Terms.Text("<") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (c, a, b) => PrecedenceAst.Conditional(c, a, b), rightAssociative: true)
            .LeftAssociative((Terms.Text("??"), static (a, b) => PrecedenceAst.Binary("??", a, b)))
            .RightAssociative(new[] { Terms.Text("="), Terms.Text("+=") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b));

        return WithRest(expression);
    }

    [GenerateParser(nameof(TryParseValuedEmpty))]
    private static Parser<string> BuildValuedEmpty() =>
        WithRest(Literals.Text("x").Optional().Then(static x => x.HasValue ? "x" : "").Operators()
            .Postfix((Literals.Text("!").Optional(), static (x, op) => op.HasValue ? PrecedenceAst.Suffix("!", x) : x))
            .LeftAssociative(new[] { Literals.Text("+").Optional() }, static (a, b, _) => PrecedenceAst.Binary("+", a, b))
            .RightAssociative(new[] { Literals.Text("^").Optional() }, static (a, b, _) => PrecedenceAst.Binary("^", a, b)));

    private static Parser<string> WithRest(Parser<string> parser) =>
        parser.And(Literals.Pattern(static _ => true, minSize: 0)).Then(static x => x.Item1 + " | " + x.Item2.ToString());
}
