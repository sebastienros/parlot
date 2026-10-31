using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Measures Memoize(), each grammar without and with it:
// - SharedPrefix: statements whose alternatives start with the same member access, like the assignments, calls and
//   expression statements of a C-like language. An alternative which fails after the member access parses it again,
//   unless it's memoized: 2 parses per statement on average instead of 1.
// - Nested: the same choice in a recursive grammar, where each alternative of a nesting level parses the inner levels
//   again: 3^8 parses of the innermost group without memoization.
// - NoReuse: a list of integers, each parsed once: the memoized parser only adds a lookup and a record to each parse,
//   the overhead of memoizing a parser that is never parsed again, here a cheap one.
// Each parse creates its context, as an application does, so the tables of the memoized parsers are allocated each time.

[MemoryDiagnoser, ShortRunJob]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
public class MemoizeBenchmarks
{
    internal static readonly Parser<int> Statements = CreateStatements(memoize: false);
    internal static readonly Parser<int> MemoizedStatements = CreateStatements(memoize: true);
    internal static readonly Parser<int> Nested = CreateNested(memoize: false);
    internal static readonly Parser<int> MemoizedNested = CreateNested(memoize: true);
    internal static readonly Parser<IReadOnlyList<long>> Integers = CreateIntegers(memoize: false);
    internal static readonly Parser<IReadOnlyList<long>> MemoizedIntegers = CreateIntegers(memoize: true);

    internal static readonly string StatementsText = CreateStatementsText(300);
    internal static readonly string NestedText = new string('(', 8) + "1" + new string(')', 8) + "?";
    internal static readonly string IntegersText = string.Join(", ", Enumerable.Range(0, 1000));

    [GlobalSetup]
    public void Setup()
    {
        if (SharedPrefix() != SharedPrefixMemoized() || SharedPrefix() != 300 * 4)
        {
            throw new InvalidOperationException("Incorrect statements parsing.");
        }

        if (NestedGroups() != NestedGroupsMemoized() || NestedGroups() != 384)
        {
            throw new InvalidOperationException("Incorrect nested parsing.");
        }

        if (NoReuse().Count != 1000 || NoReuseMemoized().Count != 1000)
        {
            throw new InvalidOperationException("Incorrect integers parsing.");
        }
    }

    // a.b.c = 1; d.e.f.g(); h.i;
    private static Parser<int> CreateStatements(bool memoize)
    {
        var target = Separated(Terms.Char('.'), Terms.Identifier()).Then(static x => x.Count);

        if (memoize)
        {
            target = target.Memoize();
        }

        var assignment = target.AndSkip(Terms.Char('=')).And(Terms.Integer()).Then(static x => x.Item1 + (int)x.Item2);
        var call = target.AndSkip(Terms.Char('(')).AndSkip(Terms.Char(')')).Then(static x => x + 1);
        var statement = OneOf(assignment, call, target).AndSkip(Terms.Char(';'));

        return ZeroOrMany(statement).Then(static x =>
        {
            var sum = 0;

            for (var i = 0; i < x.Count; i++)
            {
                sum += x[i];
            }

            return sum;
        });
    }

    internal static string CreateStatementsText(int count)
    {
        string[] statements = ["a.b.c = 1;", "d.e.f();", "g.h.i.j;"];

        return string.Join("\n", Enumerable.Range(0, count).Select(i => statements[i % statements.Length]));
    }

    // ((1)!)?
    private static Parser<int> CreateNested(bool memoize)
    {
        var value = Deferred<int>();
        var group = Between(Terms.Char('('), value, Terms.Char(')'));

        if (memoize)
        {
            group = group.Memoize();
        }

        value.Parser = OneOf(
            group.AndSkip(Terms.Char('!')).Then(static x => x * 2),
            group.AndSkip(Terms.Char('?')).Then(static x => x * 3),
            group.Then(static x => x * 2),
            Terms.Integer().Then(static x => (int)x));

        return value;
    }

    private static Parser<IReadOnlyList<long>> CreateIntegers(bool memoize)
    {
        var integer = Terms.Integer();

        return Separated(Terms.Char(','), memoize ? integer.Memoize() : integer);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("SharedPrefix")]
    public int SharedPrefix() => Statements.Parse(StatementsText);

    [Benchmark, BenchmarkCategory("SharedPrefix")]
    public int SharedPrefixMemoized() => MemoizedStatements.Parse(StatementsText);

    [Benchmark(Baseline = true), BenchmarkCategory("Nested")]
    public int NestedGroups() => Nested.Parse(NestedText);

    [Benchmark, BenchmarkCategory("Nested")]
    public int NestedGroupsMemoized() => MemoizedNested.Parse(NestedText);

    [Benchmark(Baseline = true), BenchmarkCategory("NoReuse")]
    public IReadOnlyList<long> NoReuse() => Integers.Parse(IntegersText);

    [Benchmark, BenchmarkCategory("NoReuse")]
    public IReadOnlyList<long> NoReuseMemoized() => MemoizedIntegers.Parse(IntegersText);
}
