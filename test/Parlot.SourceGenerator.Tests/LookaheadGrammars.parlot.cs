#nullable enable

using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Sequences which start with a lookahead, Not(x).SkipAnd(y) and Peek(x).SkipAnd(y), in choices which select them with the
// chars y starts with: the generated lookaheads test the char before invoking x. Each has a string and a TextReader entry
// point returning the furthest failure, compared with the runtime parser in LookaheadSourceTests.
public static partial class LookaheadGrammars
{
    // A statement list: 'let' declarations, and expression statements which don't start with a reserved word or a '{'
    // (ECMAScript's [lookahead ∉ { ... }]). The expression is a Deferred set after the statement is built.
    [GenerateParser(nameof(TryParseStatements))]
    public static Parser<string> BuildStatements()
    {
        var expression = Deferred<string>();
        var reserved = OneOf(Terms.Keyword("let"), Terms.Keyword("if"), Terms.Text("{"));

        var statement = OneOf(
            Terms.Keyword("let").SkipAnd(Terms.Identifier()).Then(static x => "let " + x),
            Terms.Text("if").SkipAnd(expression).AndSkip(Terms.Char(';')).Then(static x => "if " + x),
            Not(reserved).SkipAnd(expression).AndSkip(Terms.Char(';')).Then(static x => "expr " + x));

        expression.Parser = Terms.Identifier().Then(static x => x.ToString())
            .Or(Between(Terms.Char('('), expression, Terms.Char(')')).Then(static x => $"({x})"));

        return ZeroOrMany(statement).AndSkip(Terms.Char('}')).Then(static x => string.Join(",", x));
    }

    // A positive lookahead: an identifier which starts with an 'a' or a 'b', or a number
    [GenerateParser(nameof(TryParsePeeked))]
    public static Parser<string> BuildPeeked()
    {
        var choice = OneOf(
            Peek(Terms.Char('a').Or(Terms.Char('b'))).SkipAnd(Terms.Identifier()).Then(static x => "id " + x),
            Terms.Integer().Then(static x => "int " + x),
            Terms.Char(';').Then(static _ => ";"));

        return ZeroOrMany(choice).AndSkip(Terms.Char('}')).Then(static x => string.Join(",", x));
    }

    // Without white space: the lookaheads and what follows them are literals
    [GenerateParser(nameof(TryParseLiterals))]
    public static Parser<string> BuildLiterals()
    {
        var word = Literals.Pattern(static c => c is >= 'a' and <= 'z');
        var choice = OneOf(
            Not(Literals.Text("let")).SkipAnd(word).Then(static x => "word " + x),
            Literals.Text("let").Then(static _ => "let"),
            Literals.Char(' ').Then(static _ => "_"),
            Peek(Literals.Char('(')).SkipAnd(Literals.Char('(').SkipAnd(word).AndSkip(Literals.Char(')'))).Then(static x => $"({x})"));

        return ZeroOrMany(choice).AndSkip(Literals.Char('}')).Then(static x => string.Join(",", x));
    }
}
