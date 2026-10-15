using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method              | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
// |-------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
// | Identifier          | 16.12 us | 0.091 us | 0.085 us | 3.5095 | 0.2136 |  32.36 KB |
// | IdentifierWithChars | 15.97 us | 0.054 us | 0.050 us | 3.5095 | 0.2136 |  32.36 KB |

// Measures a choice whose first alternative is an identifier, on tokens which can't start one. The identifier is only
// skipped when the choice knows which chars it can start with.

[MemoryDiagnoser, ShortRunJob]
public class IdentifierSeekBenchmarks
{
    private const int Tokens = 1_000;

    private static readonly Parser<IReadOnlyList<TextSpan>> _identifier = Create(Terms.Identifier());
    private static readonly Parser<IReadOnlyList<TextSpan>> _identifierWithChars = Create(Terms.Identifier(Character.DefaultIdentifierStart, Character.DefaultIdentifierPart));

    private readonly string _text = CreateText();

    private static Parser<IReadOnlyList<TextSpan>> Create(Parser<TextSpan> identifier) => ZeroOrMany(OneOf(
        identifier,
        Terms.String(),
        Terms.Char('(').Then(static _ => new TextSpan("(")),
        Terms.Char(')').Then(static _ => new TextSpan(")"))));

    private static string CreateText()
    {
        var text = string.Concat(System.Linq.Enumerable.Repeat("(\"a\") ", Tokens / 3));

        if (_identifier.Parse(text).Count != Tokens / 3 * 3)
        {
            throw new InvalidOperationException("Incorrect token parsing.");
        }

        return text;
    }

    [Benchmark]
    public IReadOnlyList<TextSpan> Identifier() => _identifier.Parse(_text);

    [Benchmark]
    public IReadOnlyList<TextSpan> IdentifierWithChars() => _identifierWithChars.Parse(_text);
}
