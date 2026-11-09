#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The tokens of a lexer as the longest match of their parsers, in the order of a grammar, against the ordered choice whose
// alternatives were sorted by hand so that the first match is the longest one: the longest texts first, and numbers read
// by the decimal parser only. The exclusive tokens start with distinct chars, so that each char selects one alternative
// in both choices.
[MemoryDiagnoser, ShortRunJob]
public partial class LongestBenchmarks
{
    public static readonly string Source = string.Concat(Enumerable.Repeat(
        "in int i <<= 1 <= 12 1.5 < << total += 3.25 ; x <<= 42 + index in y ;\n", 50)).TrimEnd();

    public static readonly Parser<int> RuntimeOneOf = CreateOneOf();

    public static readonly Parser<int> RuntimeLongest = CreateLongest();

    public static readonly string ExclusiveSource = string.Concat(Enumerable.Repeat(
        "total ; 12 < index ; 42 < count ; first < 7 ; second < third ;\n", 50)).TrimEnd();

    public static readonly Parser<int> RuntimeExclusiveOneOf = CreateExclusive(static (a, b, c, d) => Parsers.OneOf(a, b, c, d));

    public static readonly Parser<int> RuntimeExclusiveLongest = CreateExclusive(static (a, b, c, d) => Parsers.Longest(a, b, c, d));

    public static partial bool TryParseOneOf(string text, out int value);
    public static partial bool TryParseLongest(string text, out int value);
    public static partial bool TryParseExclusiveOneOf(string text, out int value);
    public static partial bool TryParseExclusiveLongest(string text, out int value);

    [Benchmark(Baseline = true)]
    public int OneOf() => RuntimeOneOf.Parse(Source);

    [Benchmark]
    public int Longest() => RuntimeLongest.Parse(Source);

    [Benchmark]
    public int OneOfGenerated() => TryParseOneOf(Source, out var value) ? value : -1;

    [Benchmark]
    public int LongestGenerated() => TryParseLongest(Source, out var value) ? value : -1;

    [Benchmark]
    public int ExclusiveOneOf() => RuntimeExclusiveOneOf.Parse(ExclusiveSource);

    [Benchmark]
    public int ExclusiveLongest() => RuntimeExclusiveLongest.Parse(ExclusiveSource);

    [Benchmark]
    public int ExclusiveOneOfGenerated() => TryParseExclusiveOneOf(ExclusiveSource, out var value) ? value : -1;

    [Benchmark]
    public int ExclusiveLongestGenerated() => TryParseExclusiveLongest(ExclusiveSource, out var value) ? value : -1;

    private static Parser<int> CreateExclusive(Func<Parser<int>, Parser<int>, Parser<int>, Parser<int>, Parser<int>> choice) => ZeroOrMany(choice(
        Terms.Char(';').Then(static _ => 7),
        Terms.Char('<').Then(static _ => 4),
        Terms.Identifier().Then(static _ => 9),
        Terms.Integer().Then(static _ => 10))).Eof().Then(static tokens => Count(tokens));

    private static Parser<int> CreateOneOf() => ZeroOrMany(Parsers.OneOf(
        Terms.Text("<<=").Then(static _ => 1),
        Terms.Text("<<").Then(static _ => 2),
        Terms.Text("<=").Then(static _ => 3),
        Terms.Text("<").Then(static _ => 4),
        Terms.Text("+=").Then(static _ => 5),
        Terms.Text("+").Then(static _ => 6),
        Terms.Char(';').Then(static _ => 7),
        Terms.Keyword("in").Then(static _ => 8),
        Terms.Identifier().Then(static _ => 9),
        Terms.Decimal().Then(static _ => 10))).Eof().Then(static tokens => Count(tokens));

    private static Parser<int> CreateLongest() => ZeroOrMany(Parsers.Longest(
        Terms.Text("<").Then(static _ => 4),
        Terms.Text("<=").Then(static _ => 3),
        Terms.Text("<<").Then(static _ => 2),
        Terms.Text("<<=").Then(static _ => 1),
        Terms.Text("+").Then(static _ => 6),
        Terms.Text("+=").Then(static _ => 5),
        Terms.Char(';').Then(static _ => 7),
        Terms.Keyword("in").Then(static _ => 8),
        Terms.Identifier().Then(static _ => 9),
        Terms.Integer().Then(static _ => 10),
        Terms.Decimal().Then(static _ => 10))).Eof().Then(static tokens => Count(tokens));

    // A checksum of the kinds of the tokens, which tells when a choice didn't keep the right one
    public static int Count(IReadOnlyList<int> tokens)
    {
        var sum = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            sum = unchecked((sum * 31) + tokens[i]);
        }

        return sum;
    }
}
