using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Parsers whose value isn't used, so their lists aren't built: captured sequences and lists, skipped lists, and TryMatch
// compared to TryParse.

[MemoryDiagnoser, ShortRunJob]
public class ParseWithoutValueBenchmarks
{
    private const string Dotted = "alpha.beta.gamma.delta.epsilon.zeta";

    private const string Statements = "a;; b; c;;; d; e;; f; g;;; h";

    // A dotted name, as a sequence with a list
    private static readonly Parser<(TextSpan, IReadOnlyList<TextSpan>)> _sequence =
        Literals.Identifier().And(ZeroOrMany(Literals.Char('.').SkipAnd(Literals.Identifier())));

    private static readonly Parser<TextSpan> _capturedSequence = Capture(_sequence);

    // The same dotted name, as a separated list
    private static readonly Parser<TextSpan> _capturedSeparated = Capture(Separated(Literals.Char('.'), Literals.Identifier()));

    // Statements followed by one or more semicolons, which are skipped
    private static readonly Parser<IReadOnlyList<TextSpan>> _skippedList = ZeroOrMany(Terms.Identifier().AndSkip(OneOrMany(Terms.Char(';'))))
        .And(Terms.Identifier()).Then(static x => x.Item1);

    [GlobalSetup]
    public void Setup()
    {
        if (CapturedSequence().ToString() != Dotted || CapturedSeparated().ToString() != Dotted || SkippedList().Count != 7
            || !TryParseSequence() || TryMatchSequence().ToString() != Dotted)
        {
            throw new InvalidOperationException("Incorrect parsing.");
        }
    }

    [Benchmark]
    public TextSpan CapturedSequence() => _capturedSequence.Parse(new ParseContext(new Scanner(Dotted)));

    [Benchmark]
    public TextSpan CapturedSeparated() => _capturedSeparated.Parse(new ParseContext(new Scanner(Dotted)));

    // The value is built, and discarded
    [Benchmark]
    public bool TryParseSequence() => _sequence.TryParse(Dotted, out _);

    [Benchmark]
    public TextSpan TryMatchSequence() => _sequence.TryMatch(Dotted, out var match) ? match : default;

    [Benchmark]
    public IReadOnlyList<TextSpan> SkippedList() => _skippedList.Parse(new ParseContext(new Scanner(Statements)))!;
}
