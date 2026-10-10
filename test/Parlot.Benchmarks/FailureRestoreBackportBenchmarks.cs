using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class FailureRestoreBackportBenchmarks
{
    private ParseContext _context;
    private Parser<string> _eof;
    private Parser<string> _switch;
    [GlobalSetup]
    public void Setup()
    {
        _context = new ParseContext(new Scanner("abc"));
        _eof = Literals.Text("ab").Eof();
        var next = Literals.Text("b");
        _switch = Literals.Text("a").Switch((_, _) => next);
    }
    private bool Parse(Parser<string> parser)
    {
        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<string>();
        return parser.Parse(_context, ref result);
    }
    [Benchmark]
    public bool EofFailure() => Parse(_eof);
    [Benchmark]
    public bool SwitchSuccess() => Parse(_switch);
}
