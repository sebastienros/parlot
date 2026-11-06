#nullable enable

using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// An identifier reference rejected when it is a reserved word and converted to a node: When(...).Then(...) against the
// fused ThenTry(...), on a token stream where most tokens are identifiers
[MemoryDiagnoser, ShortRunJob]
public partial class ThenTryBenchmarks
{
    public static readonly string Source = string.Concat(Enumerable.Repeat(
        "var total = sum ( first , second ) ; if ( total ) return scale ( total , factor , offset ) ; result = total ;\n", 50)).TrimEnd();

    public static readonly Parser<object> RuntimeWhenThenToken = Terms.Identifier()
        .When(static (_, name) => !IsReserved(name.Span))
        .Then<object>(static (_, start, end, name) => new Name(start, end));

    public static readonly Parser<object> RuntimeThenTryToken = Terms.Identifier()
        .ThenTry(static (ParseContext _, int start, int end, TextSpan name, out object result) =>
        {
            if (IsReserved(name.Span))
            {
                result = null!;
                return false;
            }

            result = new Name(start, end);
            return true;
        });

    public static readonly Parser<object> RuntimeWhenThen = CreateTokens(RuntimeWhenThenToken);

    public static readonly Parser<object> RuntimeThenTry = CreateTokens(RuntimeThenTryToken);

    public static partial bool TryParseWhenThen(string text, out object value);
    public static partial bool TryParseThenTry(string text, out object value);
    public static partial bool TryParseWhenThenToken(string text, out object value);
    public static partial bool TryParseThenTryToken(string text, out object value);

    // A single identifier, accepted, and a reserved word, rejected
    [Benchmark]
    public object? WhenThenToken() => RuntimeWhenThenToken.Parse("  total");

    [Benchmark]
    public object? ThenTryToken() => RuntimeThenTryToken.Parse("  total");

    [Benchmark]
    public object? WhenThenTokenRejected() => RuntimeWhenThenToken.Parse("  return");

    [Benchmark]
    public object? ThenTryTokenRejected() => RuntimeThenTryToken.Parse("  return");

    [Benchmark]
    public object? WhenThenTokenGenerated() => TryParseWhenThenToken("  total", out var value) ? value : null;

    [Benchmark]
    public object? ThenTryTokenGenerated() => TryParseThenTryToken("  total", out var value) ? value : null;

    [Benchmark]
    public bool WhenThenTokenRejectedGenerated() => TryParseWhenThenToken("  return", out _);

    [Benchmark]
    public bool ThenTryTokenRejectedGenerated() => TryParseThenTryToken("  return", out _);

    // The tokens of a statement list

    [Benchmark]
    public object? WhenThen() => RuntimeWhenThen.Parse(Source);

    [Benchmark]
    public object? ThenTry() => RuntimeThenTry.Parse(Source);

    [Benchmark]
    public object? WhenThenGenerated() => TryParseWhenThen(Source, out var value) ? value : null;

    [Benchmark]
    public object? ThenTryGenerated() => TryParseThenTry(Source, out var value) ? value : null;

    // The tokens of a statement list, the reserved words being the alternatives after the identifier
    private static Parser<object> CreateTokens(Parser<object> identifier) =>
        ZeroOrMany(OneOf(
            identifier,
            Terms.Text("var").Then<object>(static _ => "var"),
            Terms.Text("if").Then<object>(static _ => "if"),
            Terms.Text("return").Then<object>(static _ => "return"),
            Terms.Char('=').Then<object>(static _ => "="),
            Terms.Char('(').Then<object>(static _ => "("),
            Terms.Char(')').Then<object>(static _ => ")"),
            Terms.Char(',').Then<object>(static _ => ","),
            Terms.Char(';').Then<object>(static _ => ";")))
        .Eof()
        .Then<object>(static tokens => tokens.Count);

    public static bool IsReserved(ReadOnlySpan<char> span)
        => span.SequenceEqual("var".AsSpan()) || span.SequenceEqual("if".AsSpan()) || span.SequenceEqual("return".AsSpan());

    public sealed class Name
    {
        public Name(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }
}
