using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class WhiteSpaceCacheBackportBenchmarks
{
    private ParseContext _context;
    private Parser<string> _parser;
    [GlobalSetup]
    public void Setup()
    {
        _context = new ParseContext(new Scanner("  a"));
        _parser = Terms.Text("a").WithWhiteSpaceParser(Literals.WhiteSpace());
    }
    [Benchmark]
    public bool CustomWhiteSpace()
    {
        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<string>();
        return _parser.Parse(_context, ref result);
    }
}
