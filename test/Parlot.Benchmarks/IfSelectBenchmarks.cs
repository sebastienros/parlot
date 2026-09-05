using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.SourceGenerator;
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
    private Parser<string> _ifGenerated;
    private Parser<string> _selectGenerated;
    private Parser<string> _ifElseGenerated;
    private Parser<string> _selectElseGenerated;
    private ParseContext _context;

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
        _ifGenerated = CreateIfGenerated(Condition);
        _selectGenerated = CreateSelectGenerated(Condition);
        _ifElseGenerated = CreateIfElseGenerated(Condition);
        _selectElseGenerated = CreateSelectElseGenerated(Condition);
        _context = new ParseContext(new Scanner(Match ? (Condition ? "42" : "17") : "x"));

        if (_ifGenerated.GetType() == _if.GetType() || _selectGenerated.GetType() == _select.GetType()
            || _ifElseGenerated.GetType() == _ifElse.GetType() || _selectElseGenerated.GetType() == _selectElse.GetType())
        {
            throw new InvalidOperationException("Conditional parser benchmark requires source-generated factory interception.");
        }

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
    public bool IfGenerated() => Parse(_ifGenerated);

    [Benchmark, BenchmarkCategory("Guard")]
    public bool SelectGenerated() => Parse(_selectGenerated);

    [Benchmark(Baseline = true), BenchmarkCategory("Branches")]
    public bool IfElseRuntime() => Parse(_ifElse);

    [Benchmark, BenchmarkCategory("Branches")]
    public bool SelectElseRuntime() => Parse(_selectElse);

    [Benchmark, BenchmarkCategory("Branches")]
    public bool IfElseGenerated() => Parse(_ifElseGenerated);

    [Benchmark, BenchmarkCategory("Branches")]
    public bool SelectElseGenerated() => Parse(_selectElseGenerated);

    private bool Parse(Parser<string> parser)
    {
        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<string>();
        return parser.Parse(_context, ref result);
    }

    private static Parser<string> CreateIf(bool condition) =>
        If(() => condition, Literals.Text("42"));

    private static Parser<string> CreateSelect(bool condition) =>
        Select(() => condition ? 0 : -1, Literals.Text("42"));

    private static Parser<string> CreateIfElse(bool condition) =>
        If(() => condition, Literals.Text("42"), Literals.Text("17"));

    private static Parser<string> CreateSelectElse(bool condition) =>
        Select(() => condition ? 0 : 1, Literals.Text("42"), Literals.Text("17"));

    [GenerateParser]
    private static Parser<string> CreateIfGenerated(bool condition) =>
        If(() => condition, Literals.Text("42"));

    [GenerateParser]
    private static Parser<string> CreateSelectGenerated(bool condition) =>
        Select(() => condition ? 0 : -1, Literals.Text("42"));

    [GenerateParser]
    private static Parser<string> CreateIfElseGenerated(bool condition) =>
        If(() => condition, Literals.Text("42"), Literals.Text("17"));

    [GenerateParser]
    private static Parser<string> CreateSelectElseGenerated(bool condition) =>
        Select(() => condition ? 0 : 1, Literals.Text("42"), Literals.Text("17"));
}
