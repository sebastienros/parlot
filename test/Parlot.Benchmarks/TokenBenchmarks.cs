using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The cost of AsToken, which forgets the failures its parser records: dotted names read with and without it
[MemoryDiagnoser, ShortRunJob]
public partial class TokenBenchmarks
{
    public static partial bool TryParseNames(string text, out int value);
    public static partial bool TryParseTokens(string text, out int value);

    // Each name tries a name after a '.' which isn't there before the '+'
    internal static readonly string NamesText = string.Join("+", System.Linq.Enumerable.Repeat("a.bc.def+x+y.zz", 40));

    // The last name ends with a '.': the parse fails at the end
    internal static readonly string RejectedText = NamesText + "+x.";

    private static readonly Parser<int> NamesParser = CreateNames(asToken: false);
    private static readonly Parser<int> TokensParser = CreateNames(asToken: true);

    // The runtime copy of the grammars of TokenBenchmarks.parlot.cs
    internal static Parser<int> CreateNames(bool asToken)
    {
        var name = Capture(Separated(Literals.Char('.'), Literals.Identifier()));

        return Separated(Literals.Char('+'), asToken ? AsToken(name) : name).Then(static names => names.Count).Eof();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Runtime")]
    public int Names_Runtime() => NamesParser.Parse(NamesText);

    [Benchmark, BenchmarkCategory("Runtime")]
    public int Tokens_Runtime() => TokensParser.Parse(NamesText);

    [Benchmark, BenchmarkCategory("Runtime")]
    public bool NamesRejected_Runtime() => NamesParser.TryParse(RejectedText, out _);

    [Benchmark, BenchmarkCategory("Runtime")]
    public bool TokensRejected_Runtime() => TokensParser.TryParse(RejectedText, out _);

    [Benchmark, BenchmarkCategory("Generated")]
    public int Names_Generated() => TryParseNames(NamesText, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public int Tokens_Generated() => TryParseTokens(NamesText, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Generated")]
    public bool NamesRejected_Generated() => TryParseNames(RejectedText, out _);

    [Benchmark, BenchmarkCategory("Generated")]
    public bool TokensRejected_Generated() => TryParseTokens(RejectedText, out _);
}
