using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using System;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser]
public partial class CollectionBenchmarks
{
    private Parser<IReadOnlyList<(int, string)>> _runtime;
    private Parser<IReadOnlyList<(int, string)>> _generated;
    private ParseContext _context;

    [Params(0, 1, 4, 5, 32)]
    public int Count { get; set; }

    [Params("ZeroOrMany", "OneOrMany", "Separated")]
    public string Combinator { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Func<Parser<IReadOnlyList<(int, string)>>> factory = Combinator switch
        {
            "ZeroOrMany" => CreateZero,
            "OneOrMany" => CreateOne,
            _ => CreateSeparated
        };
        _runtime = factory();
        _generated = Combinator switch
        {
            "ZeroOrMany" => CreateZero(),
            "OneOrMany" => CreateOne(),
            _ => CreateSeparated()
        };
        var input = new string('x', Count);
        if (Combinator == "Separated")
        {
            input = string.Join(",", input.ToCharArray());
        }
        _context = new ParseContext(new Scanner(input));

        if (_runtime.GetType() == _generated.GetType())
        {
            throw new InvalidOperationException("Collection benchmarks require factory interception.");
        }

        var expectedSuccess = Count > 0 || Combinator == "ZeroOrMany";
        if (Runtime() != expectedSuccess || Generated() != expectedSuccess)
        {
            throw new InvalidOperationException("Unexpected collection parser result.");
        }
    }

    [Benchmark(Baseline = true)]
    public bool Runtime() => Parse(_runtime);

    [Benchmark]
    public bool Generated() => Parse(_generated);

    private bool Parse(Parser<IReadOnlyList<(int, string)>> parser)
    {
        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<IReadOnlyList<(int, string)>>();
        return parser.Parse(_context, ref result);
    }

    [GenerateParser]
    private static Parser<IReadOnlyList<(int, string)>> CreateZero() =>
        ZeroOrMany(Literals.Text("x").Then(static text => (1, text)));

    [GenerateParser]
    private static Parser<IReadOnlyList<(int, string)>> CreateOne() =>
        OneOrMany(Literals.Text("x").Then(static text => (1, text)));

    [GenerateParser]
    private static Parser<IReadOnlyList<(int, string)>> CreateSeparated() =>
        Separated(Literals.Char(','), Literals.Text("x").Then(static text => (1, text)));
}
