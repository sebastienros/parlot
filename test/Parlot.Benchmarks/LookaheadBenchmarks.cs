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
    public static partial bool TryParsePeekBlocks(string text, out int value);
    public static partial bool TryParseFollowedBlocks(string text, out int value);

    private static readonly Parser<int> PeekBlocks = CreateBlocks(usePeek: true);
    private static readonly Parser<int> FollowedBlocks = CreateBlocks(usePeek: false);

    // The last statement of each block has no ';'
    internal static readonly string BlocksText = string.Join("\n", System.Linq.Enumerable.Repeat("{ a; bb; ccc } { d } { e; f }", 50));

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

    // The same grammars at runtime, see LookaheadBenchmarks.parlot.cs
    private static readonly Parser<string> FollowedParser =
        Terms.Identifier().WhenFollowedBy(Terms.Char('(')).Then(static value => "");

    private static readonly Parser<string> NotFollowedParser =
        Terms.Identifier().WhenNotFollowedBy(Terms.Char('(')).Then(static value => "");

    private static readonly Parser<string> NotParser =
        Not(Terms.Text("from")).SkipAnd(Terms.Identifier()).Then(static value => "");

    private static readonly Parser<string> WhenParser =
        Terms.Identifier().When(static (_, value) => value.Length == 0 || value.Span[0] != 'f').Then(static value => "");

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
    [Benchmark, BenchmarkCategory("Peek")]
    public int AlwaysWhenFollowedBy_Runtime() => FollowedBlocks.Parse(BlocksText);

    [Benchmark, BenchmarkCategory("Peek")]
    public int Peek_Runtime() => PeekBlocks.Parse(BlocksText);

    [Benchmark, BenchmarkCategory("Peek")]
    public int AlwaysWhenFollowedBy_Generated() => TryParseFollowedBlocks(BlocksText, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Peek")]
    public int Peek_Generated() => TryParsePeekBlocks(BlocksText, out var value) ? value : -1;
}
