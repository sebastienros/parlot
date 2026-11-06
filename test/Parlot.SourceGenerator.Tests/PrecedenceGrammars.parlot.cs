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

    // Only prefix levels, after which no operator is looked up, the same table as PrecedenceTests.RuntimePrefixOnly
    [GenerateParser(nameof(TryParsePrefixOnly))]
    private static Parser<string> BuildPrefixOnly()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)), (Terms.Char('~'), static x => PrecedenceAst.Prefix("~", x)))
            .Unary((Terms.Text("not"), static x => PrecedenceAst.Prefix("not ", x)));

        return WithRest(expression);
    }

    // A binary level as the tightest one and a looser prefix level, the same tables as PrecedenceTests.RuntimeTightest
    [GenerateParser(nameof(TryParseTightestLeft))]
    private static Parser<string> BuildTightestLeft()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .LeftAssociative((Terms.Char('*'), static (a, b) => PrecedenceAst.Binary("*", a, b)), (Terms.Char('/'), static (a, b) => PrecedenceAst.Binary("/", a, b)))
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)))
            .LeftAssociative((Terms.Char('+'), static (a, b) => PrecedenceAst.Binary("+", a, b)));

        return WithRest(expression);
    }

    [GenerateParser(nameof(TryParseTightestRight))]
    private static Parser<string> BuildTightestRight()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .RightAssociative((Terms.Char('*'), static (a, b) => PrecedenceAst.Binary("*", a, b)), (Terms.Char('/'), static (a, b) => PrecedenceAst.Binary("/", a, b)))
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)))
            .LeftAssociative((Terms.Char('+'), static (a, b) => PrecedenceAst.Binary("+", a, b)));

        return WithRest(expression);
    }

    [GenerateParser(nameof(TryParseTightestValued))]
    private static Parser<string> BuildTightestValued()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .LeftAssociative(new[] { Terms.Text("*"), Terms.Text("/") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)))
            .LeftAssociative((Terms.Char('+'), static (a, b) => PrecedenceAst.Binary("+", a, b)));

        return WithRest(expression);
    }

    // A table without levels parses its operand
    [GenerateParser(nameof(TryParseNoLevels))]
    private static Parser<string> BuildNoLevels() =>
        WithRest(Terms.Identifier().Then(static x => x.ToString()).Operators());

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

    // Valued prefix levels mixed with a plain one and postfix levels, and factories receiving the context. The runtime
    // parser is the same, see PrecedenceTests.
    [GenerateParser(nameof(TryParseValuedPrefix))]
    private static Parser<string> BuildValuedPrefix()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix((Terms.Text("++"), static (ParseContext context, string x, string op) => PrecedenceAst.Suffix(op, x)))
            .Unary((Terms.Char('!'), static x => PrecedenceAst.Prefix("!", x)))
            .Unary((Terms.Char('-'), static (string x, char op) => PrecedenceAst.Prefix(op.ToString(), x)), (Terms.Char('~'), static (string x, char op) => PrecedenceAst.Prefix(op.ToString(), x)))
            .RightAssociative((Terms.Text("**"), static (ParseContext context, string a, string b) => PrecedenceAst.Binary("**", a, b)))
            .Unary(
                (Terms.Text("typeof").Then(static (_, start, _, _) => start), static (ParseContext context, string x, int start) => $"typeof@{start}({x})"),
                (Terms.Text("void").Then(static (_, start, _, _) => start), static (ParseContext context, string x, int start) => $"void@{start}({x})"))
            .LeftAssociative(new[] { Terms.Text("*"), Terms.Text("/") }, static (ParseContext context, string a, string b, string op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => PrecedenceAst.Binary("+", a, b)), (Terms.Char('-'), static (a, b) => PrecedenceAst.Binary("-", a, b)))
            .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (ParseContext context, string c, string a, string b) => PrecedenceAst.Conditional(c, a, b))
            .Unary((Terms.Text("await").Then(static (_, start, _, _) => start), static (string x, int start) => $"await@{start}({x})"));

        return WithRest(expression);
    }

    // Only prefix levels, after which no operator is looked up, one of them with values
    [GenerateParser(nameof(TryParseValuedPrefixOnly))]
    private static Parser<string> BuildValuedPrefixOnly()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString());
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Unary((Terms.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => $"-@{start}({x})"))
            .Unary((Terms.Text("not"), static x => PrecedenceAst.Prefix("not ", x)));

        return WithRest(expression);
    }

    // A single valued prefix level whose operators are counted as levels of recursion
    [GenerateParser(nameof(TryParseValuedPrefixLimited), MaxRecursionDepth = 3)]
    private static Parser<string> BuildValuedPrefixLimited() =>
        WithRest(Literals.Char('a').Then(static _ => "a").Operators()
            .Unary((Literals.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => $"-@{start}({x})"))
            .LeftAssociative((Literals.Char('*'), static (a, b) => PrecedenceAst.Binary("*", a, b))));

    // A valued prefix level whose first operator can match without reading anything
    [GenerateParser(nameof(TryParseValuedZeroWidthPrefix))]
    private static Parser<string> BuildValuedZeroWidthPrefix() =>
        WithRest(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)).Operators()
            .Unary(
                (ZeroOrMany(Literals.Char('-')).Then(static x => x.Count), static (string x, int count) => $"-{count}({x})"),
                (Literals.Char('+').Then(static (_, start, _, _) => start), static (string x, int start) => $"+@{start}({x})")));

    private static Parser<string> WithRest(Parser<string> parser) =>
        parser.And(Literals.Pattern(static _ => true, minSize: 0)).Then(static x => x.Item1 + " | " + x.Item2.ToString());
}
