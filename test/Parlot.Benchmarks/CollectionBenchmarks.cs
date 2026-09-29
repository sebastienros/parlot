using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser]
public partial class CollectionBenchmarks
{
    private Parser<IReadOnlyList<(int, string)>> _runtime;
    private string _input;

    [Params(0, 1, 4, 5, 32)]
    public int Count { get; set; }

    [Params("ZeroOrMany", "OneOrMany", "Separated", "BoundedZero", "BoundedOne", "BoundedSeparated")]
    public string Combinator { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Func<Parser<IReadOnlyList<(int, string)>>> factory = Combinator switch
        {
            "ZeroOrMany" => CreateZero,
            "OneOrMany" => CreateOne,
            "Separated" => CreateSeparated,
            "BoundedZero" => CreateBoundedZero,
            "BoundedOne" => CreateBoundedOne,
            _ => CreateBoundedSeparated
        };
        _runtime = factory();
        _input = new string('x', Count);
        if (Combinator is "Separated" or "BoundedSeparated")
        {
            _input = string.Join(",", _input.ToCharArray());
        }

        var expectedSuccess = Count > 0 || Combinator is "ZeroOrMany" or "BoundedZero";
        if (Runtime() != expectedSuccess || Generated() != expectedSuccess)
        {
            throw new InvalidOperationException("Unexpected collection parser result.");
        }
    }

    [Benchmark(Baseline = true)]
    public bool Runtime() => Parse(_runtime);

    [Benchmark]
    public bool Generated() => Combinator switch
    {
        "ZeroOrMany" => TryParseZero(_input, out _),
        "OneOrMany" => TryParseOne(_input, out _),
        "Separated" => TryParseSeparated(_input, out _),
        "BoundedZero" => TryParseBoundedZero(_input, out _),
        "BoundedOne" => TryParseBoundedOne(_input, out _),
        _ => TryParseBoundedSeparated(_input, out _)
    };

    private bool Parse(Parser<IReadOnlyList<(int, string)>> parser)
    {
        return parser.TryParse(_input, out _);
    }

    private static Parser<IReadOnlyList<(int, string)>> CreateZero() =>
        ZeroOrMany(Literals.Text("x").Then(static text => (1, text)));

    private static Parser<IReadOnlyList<(int, string)>> CreateOne() =>
        OneOrMany(Literals.Text("x").Then(static text => (1, text)));

    private static Parser<IReadOnlyList<(int, string)>> CreateSeparated() =>
        Separated(Literals.Char(','), Literals.Text("x").Then(static text => (1, text)));

    private static Parser<IReadOnlyList<(int, string)>> CreateBoundedZero() =>
        ZeroOrMany(Literals.Text("x").Then(static text => (1, text)), max: 2);

    private static Parser<IReadOnlyList<(int, string)>> CreateBoundedOne() =>
        OneOrMany(Literals.Text("x").Then(static text => (1, text)), max: 2);

    private static Parser<IReadOnlyList<(int, string)>> CreateBoundedSeparated() =>
        Separated(Literals.Char(','), Literals.Text("x").Then(static text => (1, text)), min: 1, max: 2);

    private static partial bool TryParseZero(string input, out IReadOnlyList<(int, string)> value);
    private static partial bool TryParseOne(string input, out IReadOnlyList<(int, string)> value);
    private static partial bool TryParseSeparated(string input, out IReadOnlyList<(int, string)> value);
    private static partial bool TryParseBoundedZero(string input, out IReadOnlyList<(int, string)> value);
    private static partial bool TryParseBoundedOne(string input, out IReadOnlyList<(int, string)> value);
    private static partial bool TryParseBoundedSeparated(string input, out IReadOnlyList<(int, string)> value);
}
