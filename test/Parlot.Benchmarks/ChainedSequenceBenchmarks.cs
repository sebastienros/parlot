#nullable enable

using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Statements of chained runtime sequences. A sequence of more than two parsers is the sequence of the sequence of the
// first ones and the last one: with dynamic PGO the JIT inlines the Parse of the inner sequence into the outer one, which
// a sequence choosing its start (where its first part matched nothing) must not prevent (issue #546).
[MemoryDiagnoser, ShortRunJob]
public class ChainedSequenceBenchmarks
{
    private static readonly Parser<IReadOnlyList<int>> Statements = ZeroOrMany(
        Terms.Text("define").SkipAnd(Terms.Text("table")).SkipAnd(Terms.Identifier()).AndSkip(Terms.Text(":="))
            .And(Terms.Identifier()).And(Terms.Identifier().Optional()).AndSkip(Terms.Char(';'))
            .Then(static (context, start, end, _) => end - start));

    private static readonly string Text = string.Concat(Enumerable.Repeat("define table a := b c;\n  define table d := e;\n", 20));

    [Benchmark]
    public IReadOnlyList<int>? Runtime() => Statements.TryParse(Text, out var lengths) ? lengths : null;
}
