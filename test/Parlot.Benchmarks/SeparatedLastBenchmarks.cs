using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// A list which ends with a rest element, like JavaScript's parameters <c>a, b, ...rest</c> (#588): the overload of
/// <c>Separated</c> with a last element, and the composition it replaces, which appends the rest element to a copy of the list.
/// </summary>
[MemoryDiagnoser]
public partial class SeparatedLastBenchmarks
{
    private static readonly Parser<string> Item = Terms.Identifier().Then(static x => x.ToString());
    private static readonly Parser<string> Rest = Terms.Text("...").SkipAnd(Terms.Identifier()).Then(static x => x.ToString());

    private readonly Parser<IReadOnlyList<string>> _composed = Composed(Item, Rest);
    private readonly Parser<IReadOnlyList<string>> _separated = Separated(Terms.Char(','), Item, Rest);
    private string _input = "";

    [Params(0, 2, 8)]
    public int Count { get; set; }

    [Params(false, true)]
    public bool WithRest { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var items = Enumerable.Range(0, Count).Select(static i => "a" + i);
        _input = string.Join(", ", WithRest ? items.Append("...rest") : items);

        var expected = Count + (WithRest ? 1 : 0);
        if (ComposedRuntime() != expected || SeparatedRuntime() != expected || SeparatedGenerated() != expected)
        {
            throw new InvalidOperationException("Unexpected list.");
        }
    }

    [Benchmark(Baseline = true)]
    public int ComposedRuntime() => _composed.Parse(_input)!.Count;

    [Benchmark]
    public int SeparatedRuntime() => _separated.Parse(_input)!.Count;

    [Benchmark]
    public int SeparatedGenerated() => TryParseSeparatedLast(_input, out var value) ? value.Count : -1;

    // The helper of a JavaScript grammar, before Separated could end with a last element
    private static Parser<IReadOnlyList<T>> Composed<T>(Parser<T> item, Parser<T> rest)
    {
        var tail = Terms.Char(',').SkipAnd(rest.Optional()).Optional();

        return OneOf(
                rest.Then(static x => (IReadOnlyList<T>)new[] { x }),
                Separated(Terms.Char(','), item).And(tail).Then(static x =>
                {
                    var (items, tail) = x;
                    if (!tail.HasValue || !tail.Value.HasValue)
                    {
                        return items;
                    }

                    var array = new T[items.Count + 1];
                    for (var i = 0; i < items.Count; i++)
                    {
                        array[i] = items[i];
                    }

                    array[items.Count] = tail.Value.Value;
                    return (IReadOnlyList<T>)array;
                }))
            .Else(Array.Empty<T>());
    }

    private static partial bool TryParseSeparatedLast(string input, out IReadOnlyList<string> value);
}
