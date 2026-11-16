using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class LookaheadBenchmarks
{
    [GenerateParser(nameof(TryParseFollowed))]
    private static Parser<string> BuildFollowed() =>
        Terms.Identifier().WhenFollowedBy(Terms.Char('(')).Then(static value => "");

    [GenerateParser(nameof(TryParseNotFollowed))]
    private static Parser<string> BuildNotFollowed() =>
        Terms.Identifier().WhenNotFollowedBy(Terms.Char('(')).Then(static value => "");

    [GenerateParser(nameof(TryParseNot))]
    private static Parser<string> BuildNot() =>
        Not(Terms.Text("from")).SkipAnd(Terms.Identifier()).Then(static value => "");

    [GenerateParser(nameof(TryParsePeeked))]
    private static Parser<string> BuildPeeked() =>
        Peek(Terms.Text("from")).SkipAnd(Terms.Identifier()).Then(static value => "");

    [GenerateParser(nameof(TryParsePeekBlocks))]
    private static Parser<int> BuildPeekBlocks() => Blocks(usePeek: true);

    [GenerateParser(nameof(TryParseFollowedBlocks))]
    private static Parser<int> BuildFollowedBlocks() => Blocks(usePeek: false);

    // Blocks of statements ended by an automatic semicolon: a ';', or nothing before a '}' or a ')', as CreateBlocks
    private static Parser<int> Blocks(bool usePeek)
    {
        var semicolon = usePeek
            ? OneOf(Terms.Char(';').Then(static _ => 1), Peek(Terms.Char('}')).Then(static _ => 0), Peek(Terms.Char(')')).Then(static _ => 0))
            : OneOf(Terms.Char(';').Then(static _ => 1), Always(0).WhenFollowedBy(Terms.Char('}')), Always(0).WhenFollowedBy(Terms.Char(')')));

        var statement = Terms.Identifier().SkipAnd(semicolon);
        var block = Between(Terms.Char('{'), ZeroOrMany(statement), Terms.Char('}')).Then(static statements => statements.Count);

        return ZeroOrMany(block).Then(static blocks => blocks.Count).Eof();
    }

    [GenerateParser(nameof(TryParseRestrictedStatements))]
    private static Parser<int> BuildRestrictedStatements() => RestrictedStatements();

    // Statements whose expression statements can't start with a reserved word or a '{', as CreateRestrictedStatements
    private static Parser<int> RestrictedStatements()
    {
        var statement = Deferred<int>();
        var expression = Deferred<int>();
        var reserved = OneOf(Terms.Keyword("let"), Terms.Keyword("if"), Terms.Text("{"));

        var block = Between(Terms.Char('{'), ZeroOrMany(statement), Terms.Char('}')).Then(static statements => statements.Count);
        var let = Terms.Keyword("let").SkipAnd(Terms.Identifier()).AndSkip(Terms.Char(';')).Then(static _ => 1);
        var conditional = Terms.Keyword("if").SkipAnd(Between(Terms.Char('('), expression, Terms.Char(')'))).SkipAnd(statement).Then(static x => x + 1);

        // ExpressionStatement: [lookahead ∉ { {, let, if }] Expression ;
        var expressionStatement = Not(reserved).SkipAnd(expression).AndSkip(Terms.Char(';'));

        statement.Parser = OneOf(block, let, conditional, expressionStatement);
        expression.Parser = Terms.Identifier().Then(static _ => 1).Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        return ZeroOrMany(statement).Then(static statements => statements.Count).Eof();
    }

    [GenerateParser(nameof(TryParseWhen))]
    private static Parser<string> BuildWhen() =>
        Terms.Identifier().When(static (_, value) => value.Length == 0 || value.Span[0] != 'f').Then(static value => "");

    // The same condition, which records a reason with the failure
    [GenerateParser(nameof(TryParseWhenReason))]
    private static Parser<string> BuildWhenReason() =>
        Terms.Identifier().When(static (_, value) => value.Length == 0 || value.Span[0] != 'f', "reserved").Then(static value => "");
}
