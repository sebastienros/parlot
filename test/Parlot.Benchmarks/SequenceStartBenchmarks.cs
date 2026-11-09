using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The start of a sequence whose first parser can match nothing, like optional decorators before a class: the sequence
// starts where the first parser which matched something does. At runtime each sequence compares the offsets of its first
// result, a plain sequence included. A generated callback with offsets emits such a sequence part by part.
[MemoryDiagnoser, ShortRunJob]
public partial class SequenceStartBenchmarks
{
    private static readonly Parser<IReadOnlyList<TextSpan>> Decorators = ZeroOrMany(Terms.Char('@').SkipAnd(Terms.Identifier()));

    private static readonly Parser<int> RuntimeClass = Decorators.And(Terms.Text("class")).And(Terms.Identifier())
        .Then(static (context, start, end, _) => end - start);

    private static readonly Parser<int> RuntimePlain = Terms.Text("class").And(Terms.Identifier()).And(Terms.Char('{'))
        .Then(static (context, start, end, _) => end - start);

    public static partial bool TryParseClass(string text, out int value);

    [Params("   class a", "  @a @b class a")]
    public string Input { get; set; } = "";

    [Benchmark]
    public bool Runtime() => RuntimeClass.TryParse(Input, out _);

    [Benchmark]
    public bool RuntimePlainSequence() => RuntimePlain.TryParse("  class a {", out _);

    [Benchmark]
    public bool Generated() => TryParseClass(Input, out _);
}
