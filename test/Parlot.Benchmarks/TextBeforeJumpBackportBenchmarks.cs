using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class TextBeforeJumpBackportBenchmarks
{
    private ParseContext _context;
    private Parser<TextSpan> _literal;
    private Parser<TextSpan> _term;
    [GlobalSetup]
    public void Setup()
    {
        _context = new ParseContext(new Scanner("abcdefghijklmnopqrstuvwxyz   end"));
        _literal = AnyCharBefore(Literals.Text("end"));
        _term = AnyCharBefore(Terms.Text("end"));
    }
    private bool Parse(Parser<TextSpan> parser)
    {
        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<TextSpan>();
        return parser.Parse(_context, ref result);
    }
    [Benchmark]
    public bool LiteralDelimiter() => Parse(_literal);
    [Benchmark]
    public bool WhiteSpaceDelimiter() => Parse(_term);
}
