using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// A lookahead saves and restores the furthest failure, and a rejected value computes where it starts on its failure path
[MemoryDiagnoser, ShortRunJob]
public partial class LookaheadBenchmarks
{
    public static partial bool TryParseFollowed(string text, out string value);
    public static partial bool TryParseNotFollowed(string text, out string value);
    public static partial bool TryParseNot(string text, out string value);
    public static partial bool TryParseWhen(string text, out string value);
    public static partial bool TryParseWhenReason(string text, out string value, out (int Offset, object Reason) failure);
    public static partial bool TryParsePeeked(string text, out string value);
    public static partial bool TryParsePeekBlocks(string text, out int value);
    public static partial bool TryParseFollowedBlocks(string text, out int value);
    public static partial bool TryParseRestrictedStatements(string text, out int value);

    private static readonly Parser<int> PeekBlocks = CreateBlocks(usePeek: true);
    private static readonly Parser<int> FollowedBlocks = CreateBlocks(usePeek: false);

    // The last statement of each block has no ';'
    internal static readonly string BlocksText = string.Join("\n", System.Linq.Enumerable.Repeat("{ a; bb; ccc } { d } { e; f }", 50));

    private static readonly Parser<int> RestrictedStatementsParser = CreateRestrictedStatements();

    // Blocks of statements, most of them expression statements, which a lookahead restriction keeps from starting with a
    // reserved word or a '{'
    internal static readonly string StatementsText = string.Join("\n", System.Linq.Enumerable.Repeat("{ a; let b; if (c) { d; (e); } f; } g; if (h) i; {}", 50));

    // The runtime copy of the grammar of RestrictedStatements in LookaheadBenchmarks.parlot.cs
    private static Parser<int> CreateRestrictedStatements()
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

    // The runtime copy of the grammar of Blocks in LookaheadBenchmarks.parlot.cs
    private static Parser<int> CreateBlocks(bool usePeek)
    {
        var semicolon = usePeek
            ? OneOf(Terms.Char(';').Then(static _ => 1), Peek(Terms.Char('}')).Then(static _ => 0), Peek(Terms.Char(')')).Then(static _ => 0))
            : OneOf(Terms.Char(';').Then(static _ => 1), Always(0).WhenFollowedBy(Terms.Char('}')), Always(0).WhenFollowedBy(Terms.Char(')')));

        var statement = Terms.Identifier().SkipAnd(semicolon);
        var block = Between(Terms.Char('{'), ZeroOrMany(statement), Terms.Char('}')).Then(static statements => statements.Count);

        return ZeroOrMany(block).Then(static blocks => blocks.Count).Eof();
    }

    [Benchmark]
    public bool Followed() => TryParseFollowed("  word  (", out _);

    [Benchmark]
    public bool NotFollowed() => TryParseNotFollowed("  word  ;", out _);

    [Benchmark]
    public bool Negative() => TryParseNot("  word", out _);

    [Benchmark]
    public bool WhenAccepted() => TryParseWhen("  word", out _);

    [Benchmark]
    public bool WhenRejected() => TryParseWhen("  from", out _);

    // A rejection which records a reason, returned with the failure
    [Benchmark]
    public bool WhenReasonRejected() => TryParseWhenReason("  from", out _, out _);

    // What must not follow, or must not match, is there: the failure is at its start, after the white space it skips
    [Benchmark]
    public bool NotFollowedRejected() => TryParseNotFollowed("  word  (", out _);

    [Benchmark]
    public bool NegativeRejected() => TryParseNot("  from", out _);

    // A lookahead which matches moves the cursor back and forgets what was recorded since, one which doesn't leaves its
    // failure
    [Benchmark]
    public bool Peeked() => TryParsePeeked("  from", out _);

    [Benchmark]
    public bool PeekedRejected() => TryParsePeeked("  word", out _);

    // The same grammars at runtime, see LookaheadBenchmarks.parlot.cs
    private static readonly Parser<string> FollowedParser =
        Terms.Identifier().WhenFollowedBy(Terms.Char('(')).Then(static value => "");

    private static readonly Parser<string> NotFollowedParser =
        Terms.Identifier().WhenNotFollowedBy(Terms.Char('(')).Then(static value => "");

    private static readonly Parser<string> NotParser =
        Not(Terms.Text("from")).SkipAnd(Terms.Identifier()).Then(static value => "");

    private static readonly Parser<string> WhenParser =
        Terms.Identifier().When(static (_, value) => value.Length == 0 || value.Span[0] != 'f').Then(static value => "");

    private static readonly Parser<string> WhenReasonParser =
        Terms.Identifier().When(static (_, value) => value.Length == 0 || value.Span[0] != 'f', "reserved").Then(static value => "");

    private static readonly Parser<string> PeekedParser =
        Peek(Terms.Text("from")).SkipAnd(Terms.Identifier()).Then(static value => "");

    [Benchmark]
    public bool RuntimeFollowed() => FollowedParser.TryParse("  word  (", out _);

    [Benchmark]
    public bool RuntimeNotFollowed() => NotFollowedParser.TryParse("  word  ;", out _);

    [Benchmark]
    public bool RuntimeNegative() => NotParser.TryParse("  word", out _);

    [Benchmark]
    public bool RuntimeWhenAccepted() => WhenParser.TryParse("  word", out _);

    [Benchmark]
    public bool RuntimeWhenRejected() => WhenParser.TryParse("  from", out _);

    // The error with the reason isn't built when it isn't returned
    [Benchmark]
    public bool RuntimeWhenReasonRejected() => WhenReasonParser.TryParse("  from", out _);

    [Benchmark]
    public bool RuntimeWhenReasonError() => WhenReasonParser.TryParse("  from", out _, out _);

    [Benchmark]
    public bool RuntimeNotFollowedRejected() => NotFollowedParser.TryParse("  word  (", out _);

    [Benchmark]
    public bool RuntimeNegativeRejected() => NotParser.TryParse("  from", out _);

    [Benchmark]
    public bool RuntimePeeked() => PeekedParser.TryParse("  from", out _);

    [Benchmark]
    public bool RuntimePeekedRejected() => PeekedParser.TryParse("  word", out _);

    [Benchmark, BenchmarkCategory("Peek")]
    public int AlwaysWhenFollowedBy_Runtime() => FollowedBlocks.Parse(BlocksText);

    [Benchmark, BenchmarkCategory("Peek")]
    public int Peek_Runtime() => PeekBlocks.Parse(BlocksText);

    [Benchmark, BenchmarkCategory("Peek")]
    public int AlwaysWhenFollowedBy_Generated() => TryParseFollowedBlocks(BlocksText, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Peek")]
    public int Peek_Generated() => TryParsePeekBlocks(BlocksText, out var value) ? value : -1;

    // The choice of the statements selects the expression statement with the chars an expression starts with, and the
    // lookahead doesn't invoke the reserved words where they can't start
    [Benchmark, BenchmarkCategory("Restriction")]
    public int Restriction_Runtime() => RestrictedStatementsParser.Parse(StatementsText);

    [Benchmark, BenchmarkCategory("Restriction")]
    public int Restriction_Generated() => TryParseRestrictedStatements(StatementsText, out var value) ? value : -1;
}
