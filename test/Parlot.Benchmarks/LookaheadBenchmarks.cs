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
}
