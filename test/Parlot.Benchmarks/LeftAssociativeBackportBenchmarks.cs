using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class LeftAssociativeBackportBenchmarks
{
    private ParseContext _context;
    private Parser<int> _parser;
    private Parser<int> _compiled;
    [GlobalSetup]
    public void Setup()
    {
        _context = new ParseContext(new Scanner("1+2+3"));
        _parser = Literals.Integer().Then(static x => (int)x).LeftAssociative((Literals.Char('+'), static (a, b) => a + b));
        _compiled = _parser.Compile();
    }
    private int Parse(Parser<int> parser)
    {
        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<int>();
        parser.Parse(_context, ref result);
        return result.Value;
    }
    [Benchmark]
    public int Runtime() => Parse(_parser);
    [Benchmark]
    public int Compiled() => Parse(_compiled);
}
