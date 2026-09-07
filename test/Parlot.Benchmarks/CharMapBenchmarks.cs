using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class CharMapBenchmarks
{
    private Parser<char> _parser;
    private ParseContext _context;

    [Params("a", "\u00e9", "\u4e2d")]
    public string Input { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _parser = OneOf(
            Literals.Char('a'),
            Literals.Char('\u00e9'),
            Literals.Char('\u00f1'),
            Literals.Char('\u03b1'));
        _context = new ParseContext(new Scanner(Input));
    }

    [Benchmark]
    public bool Lookup()
    {
        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<char>();
        return _parser.Parse(_context, ref result);
    }
}
