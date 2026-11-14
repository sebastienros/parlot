using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.Tests.Json;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// The cost of returning the furthest failure from a generated entry point: the overloads share the parser.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public partial class FailurePositionBenchmarks
{
#nullable disable
    private string _valid;
    private string _invalid;
    private string _calls;
    private string _words;
#nullable restore

    private const int Items = 64;

    [GlobalSetup]
    public void Setup()
    {
        _valid = JsonBench.BuildJson(4, 4, 3).ToString()!;

        // A missing comma near the end of the document
        var comma = _valid.LastIndexOf(',');
        _invalid = _valid.Remove(comma, 1);

        _calls = string.Concat(System.Linq.Enumerable.Repeat("item();", Items));
        _words = string.Concat(System.Linq.Enumerable.Repeat("item;", Items));

        if (FirstAlternative() != Items || LastAlternative() != Items || RuntimeLastAlternative() != Items)
        {
            throw new System.InvalidOperationException("The items are expected to match.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Success")]
    public bool SuccessDefault() => TryParse(_valid, out _);

    [Benchmark, BenchmarkCategory("Success")]
    public bool SuccessPosition() => TryParse(_valid, out _, out (int, int, int) _);

    [Benchmark(Baseline = true), BenchmarkCategory("Failure")]
    public bool FailureDefault() => TryParse(_invalid, out _);

    [Benchmark, BenchmarkCategory("Failure")]
    public int FailureOffset() => TryParse(_invalid, out _, out int offset) ? -1 : offset;

    [Benchmark, BenchmarkCategory("Failure")]
    public int FailurePosition() => TryParse(_invalid, out _, out (int Offset, int Line, int Column) failure) ? -1 : failure.Line;

    // The same failure with whether the input is nested too deeply, which an ordinary failure only stores
    [Benchmark, BenchmarkCategory("Failure")]
    public int FailureNesting() => TryParse(_invalid, out _, out (int Offset, bool NestedTooDeeply) failure) || failure.NestedTooDeeply ? -1 : failure.Offset;

    // The same grammar at runtime, see FailurePositionBenchmarks.parlot.cs
    private static readonly Parser<IJson> Runtime = BuildRuntimeJson();

    private static Parser<IJson> BuildRuntimeJson()
    {
        var lBrace = Terms.Char('{');
        var rBrace = Terms.Char('}');
        var lBracket = Terms.Char('[');
        var rBracket = Terms.Char(']');
        var colon = Terms.Char(':');
        var comma = Terms.Char(',');
        var json = Deferred<IJson>();

        var jsonString = Terms.String(StringLiteralQuotes.Double)
            .Then<IJson>(static value => new JsonString(value.ToString()));

        var jsonArray = Between(lBracket, Separated(comma, json), rBracket)
            .Then<IJson>(static elements => new JsonArray(elements));

        var jsonMember = Terms.String(StringLiteralQuotes.Double).And(colon).And(json)
            .Then(static member => new KeyValuePair<string, IJson>(member.Item1.ToString(), member.Item3));

        var jsonObject = Between(lBrace, Separated(comma, jsonMember), rBrace)
            .Then<IJson>(static members => new JsonObject(new Dictionary<string, IJson>(members)));

        json.Parser = OneOf(jsonString, jsonArray, jsonObject);

        return json.Eof();
    }

    [Benchmark, BenchmarkCategory("RuntimeFailure")]
    public bool RuntimeFailureDefault() => Runtime.TryParse(_invalid, out _);

    // Reads the furthest failure from the cursor of the context
    [Benchmark, BenchmarkCategory("RuntimeFailure")]
    public int RuntimeFailurePosition()
    {
        var context = new ParseContext(new Scanner(_invalid));
        return Runtime.TryParse(context, out _, out _) ? -1 : context.Scanner.Cursor.FurthestPosition.Line;
    }

    // A successful parse whose choices match with their first alternative, then with their last one, after the other
    // two read a word and failed: generated span code records where each one stopped, like the cursor of the runtime
    // parser which is moved back from there
    [Benchmark(Baseline = true), BenchmarkCategory("Alternatives")]
    public int FirstAlternative() => TryParseItems(_calls, out var count) ? count : -1;

    [Benchmark, BenchmarkCategory("Alternatives")]
    public int LastAlternative() => TryParseItems(_words, out var count) ? count : -1;

    private static readonly Parser<int> RuntimeItems =
        ZeroOrMany(OneOf(
            Literals.Identifier().AndSkip(Literals.Char('(')).AndSkip(Literals.Char(')')).Then(static _ => 1),
            Literals.Identifier().AndSkip(Literals.Char('[')).AndSkip(Literals.Char(']')).Then(static _ => 2),
            Literals.Identifier().Then(static _ => 3)).AndSkip(Literals.Char(';'))).Then(static items => items.Count).Eof();

    [Benchmark, BenchmarkCategory("Alternatives")]
    public int RuntimeLastAlternative() => RuntimeItems.TryParse(_words, out var count) ? count : -1;

    private static partial bool TryParseItems(string input, out int value);
    private static partial bool TryParse(string input, out IJson value);
    private static partial bool TryParse(string input, out IJson value, out int failureOffset);
    private static partial bool TryParse(string input, out IJson value, out (int Offset, int Line, int Column) failure);
    private static partial bool TryParse(string input, out IJson value, out (int Offset, bool NestedTooDeeply) failure);
}
