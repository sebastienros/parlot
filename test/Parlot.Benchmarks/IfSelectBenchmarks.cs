using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public partial class IfSelectBenchmarks
{
    private Parser<string> _if;
    private Parser<string> _select;
    private Parser<string> _ifElse;
    private Parser<string> _selectElse;
    private string _input;

    [Params(true, false)]
    public bool Condition { get; set; }

    [Params(true, false)]
    public bool Match { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _if = CreateIf(Condition);
        _select = CreateSelect(Condition);
        _ifElse = CreateIfElse(Condition);
        _selectElse = CreateSelectElse(Condition);
        _input = Match ? (Condition ? "42" : "17") : "x";

        if (IfRuntime() != (Condition && Match) || SelectRuntime() != (Condition && Match)
            || IfElseRuntime() != Match || SelectElseRuntime() != Match
            || IfGenerated() != (Condition && Match) || SelectGenerated() != (Condition && Match)
            || IfElseGenerated() != Match || SelectElseGenerated() != Match)
        {
            throw new InvalidOperationException("Conditional parser benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Guard")]
    public bool IfRuntime() => Parse(_if);

    [Benchmark, BenchmarkCategory("Guard")]
    public bool SelectRuntime() => Parse(_select);

    [Benchmark, BenchmarkCategory("Guard")]
    public bool IfGenerated() => TryParseIf(_input, Condition, out _);

    [Benchmark, BenchmarkCategory("Guard")]
    public bool SelectGenerated() => TryParseSelect(_input, Condition, out _);

    [Benchmark(Baseline = true), BenchmarkCategory("Branches")]
    public bool IfElseRuntime() => Parse(_ifElse);

    [Benchmark, BenchmarkCategory("Branches")]
    public bool SelectElseRuntime() => Parse(_selectElse);

    [Benchmark, BenchmarkCategory("Branches")]
    public bool IfElseGenerated() => TryParseIfElse(_input, Condition, out _);

    [Benchmark, BenchmarkCategory("Branches")]
    public bool SelectElseGenerated() => TryParseSelectElse(_input, Condition, out _);

    private bool Parse(Parser<string> parser)
    {
        return parser.TryParse(_input, out _);
    }

    private static Parser<string> CreateIf(bool condition) =>
        If(() => condition, Literals.Text("42"));

    private static Parser<string> CreateSelect(bool condition) =>
        Select(() => condition ? 0 : -1, Literals.Text("42"));

    private static Parser<string> CreateIfElse(bool condition) =>
        If(() => condition, Literals.Text("42"), Literals.Text("17"));

    private static Parser<string> CreateSelectElse(bool condition) =>
        Select(() => condition ? 0 : 1, Literals.Text("42"), Literals.Text("17"));

    private static partial bool TryParseIf(string input, bool condition, out string value);
    private static partial bool TryParseSelect(string input, bool condition, out string value);
    private static partial bool TryParseIfElse(string input, bool condition, out string value);
    private static partial bool TryParseSelectElse(string input, bool condition, out string value);
}
