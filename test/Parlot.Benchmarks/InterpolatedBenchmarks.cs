using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Text;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Measures a template-heavy input: template literals with text, escapes, '$' chars, holes holding identifiers and nested
// templates, read with the combinators a grammar writes without Interpolated (a choice per escape, per '$' and per run
// of text) and with Interpolated, which finds the end of each chunk with a vectorized search.

[MemoryDiagnoser, ShortRunJob]
public partial class InterpolatedBenchmarks
{
    private static readonly Parser<int> Combinators = CreateCombinators();
    private static readonly Parser<int> Interpolated = CreateInterpolated();

    private string _text = null!;

    // The number of chars of text in each chunk
    [Params(10, 100)]
    public int ChunkLength { get; set; }

    public const int Templates = 200;

    public const int ExpectedHoles = Templates * 3;

    [GlobalSetup]
    public void Setup()
    {
        _text = CreateText(ChunkLength);

        if (RuntimeCombinators() != ExpectedHoles || RuntimeInterpolated() != ExpectedHoles
            || GeneratedCombinators() != ExpectedHoles || GeneratedInterpolated() != ExpectedHoles)
        {
            throw new InvalidOperationException("Incorrect template parsing.");
        }
    }

    internal static string CreateText(int chunkLength)
    {
        var text = new string('a', chunkLength);
        var builder = new StringBuilder();

        for (var i = 0; i < Templates; i++)
        {
            // Three holes: an identifier, a nested template holding one, and an escape and a '$' in the text
            builder.Append("\n `").Append(text).Append(" ${ name").Append(i).Append(" } ").Append(text)
                .Append("\\n $5 ${`").Append(text).Append("${x}`} \\` ").Append(text).Append('`');
        }

        return builder.ToString();
    }

    [Benchmark(Baseline = true)]
    public int RuntimeCombinators() => Combinators.Parse(_text);

    [Benchmark]
    public int RuntimeInterpolated() => Interpolated.Parse(_text);

    [Benchmark]
    public int GeneratedCombinators()
    {
        _ = TryParseCombinators(_text, out var count);

        return count;
    }

    [Benchmark]
    public int GeneratedInterpolated()
    {
        _ = TryParseInterpolated(_text, out var count);

        return count;
    }

    public static partial bool TryParseCombinators(string text, out int value);

    public static partial bool TryParseInterpolated(string text, out int value);

    // The parsers of InterpolatedBenchmarks.parlot.cs, which isn't compiled
    internal static Parser<int> CreateCombinators()
    {
        var expression = Deferred<int>();

        var chunk = Capture(ZeroOrMany(OneOf(
            Literals.NoneOf("`$\\"),
            Capture(Literals.Char('\\').And(Literals.Pattern(static _ => true, 1, 1))),
            Capture(Literals.Char('$').WhenNotFollowedBy(Literals.Char('{'))))));

        var substitution = Between(Literals.Text("${"), expression, Terms.Char('}')).And(chunk);

        var template = Between(Terms.Char('`'), chunk.And(ZeroOrMany(substitution)), Literals.Char('`')).Then(static x =>
        {
            var count = 0;
            foreach (var hole in x.Item2)
            {
                count += hole.Item1 + 1;
            }
            return count;
        });

        expression.Parser = OneOf(Terms.Identifier().Then(static _ => 0), template);

        return ZeroOrMany(template).Then(static x =>
        {
            var count = 0;
            foreach (var item in x)
            {
                count += item;
            }
            return count;
        }).Eof();
    }

    internal static Parser<int> CreateInterpolated()
    {
        var expression = Deferred<int>();

        var template = Terms.Interpolated("`", "`", "${", "}", expression).Then(static x =>
        {
            var count = 0;
            foreach (var hole in x.Holes)
            {
                count += hole + 1;
            }
            return count;
        });

        expression.Parser = OneOf(Terms.Identifier().Then(static _ => 0), template);

        return ZeroOrMany(template).Then(static x =>
        {
            var count = 0;
            foreach (var item in x)
            {
                count += item;
            }
            return count;
        }).Eof();
    }
}
