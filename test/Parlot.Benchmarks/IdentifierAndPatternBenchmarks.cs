using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Text;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Measures a run of identifiers read by Identifier, by Identifier with extra predicates, and by Pattern, at runtime
// and with the generated parsers.

[MemoryDiagnoser, ShortRunJob]
public partial class IdentifierAndPatternBenchmarks
{
    private const int Tokens = 1_000;

    private static readonly Parser<int> _identifier = ZeroOrMany(Terms.Identifier()).Then(static tokens => tokens.Count);
    private static readonly Parser<int> _identifierWithPredicates = ZeroOrMany(Terms.Identifier(static c => c == '@', static c => c == '-')).Then(static tokens => tokens.Count);
    private static readonly Parser<int> _pattern = ZeroOrMany(Terms.Pattern(static c => Character.IsIdentifierPart(c) || c == '-')).Then(static tokens => tokens.Count);

    private readonly string _text = CreateText();

    private static string CreateText()
    {
        var builder = new StringBuilder();

        for (var i = 0; i < Tokens; i++)
        {
            builder.Append("identifier").Append(i).Append('_').Append(i % 7 == 0 ? "Long_Suffix_With_More_Chars" : "x").Append(' ');
        }

        return builder.ToString();
    }

    public IdentifierAndPatternBenchmarks()
    {
        if (Identifier() != Tokens) throw new InvalidOperationException(nameof(Identifier));
        if (IdentifierGenerated() != Tokens) throw new InvalidOperationException(nameof(IdentifierGenerated));
        if (IdentifierWithPredicates() != Tokens) throw new InvalidOperationException(nameof(IdentifierWithPredicates));
        if (IdentifierWithPredicatesGenerated() != Tokens) throw new InvalidOperationException(nameof(IdentifierWithPredicatesGenerated));
        if (Pattern() != Tokens) throw new InvalidOperationException(nameof(Pattern));
        if (PatternGenerated() != Tokens) throw new InvalidOperationException(nameof(PatternGenerated));
    }

    [Benchmark]
    public int Identifier() => _identifier.Parse(_text);

    [Benchmark]
    public int IdentifierGenerated()
    {
        _ = TryParseIdentifier(_text, out var count);
        return count;
    }

    [Benchmark]
    public int IdentifierWithPredicates() => _identifierWithPredicates.Parse(_text);

    [Benchmark]
    public int IdentifierWithPredicatesGenerated()
    {
        _ = TryParseIdentifierWithPredicates(_text, out var count);
        return count;
    }

    [Benchmark]
    public int Pattern() => _pattern.Parse(_text);

    [Benchmark]
    public int PatternGenerated()
    {
        _ = TryParsePattern(_text, out var count);
        return count;
    }

    private static partial bool TryParseIdentifier(string input, out int value);
    private static partial bool TryParseIdentifierWithPredicates(string input, out int value);
    private static partial bool TryParsePattern(string input, out int value);
}
