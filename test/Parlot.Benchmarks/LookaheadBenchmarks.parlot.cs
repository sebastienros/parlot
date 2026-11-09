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

    [GenerateParser(nameof(TryParseWhen))]
    private static Parser<string> BuildWhen() =>
        Terms.Identifier().When(static (_, value) => value.Length == 0 || value.Span[0] != 'f').Then(static value => "");
}
