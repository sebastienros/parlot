using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// Measures a single <see cref="KeywordLiteral"/> with the default ASCII-letter boundary and with a
/// custom set of identifier chars, on a match and on a rejected identifier prefix. The contexts are
/// reused so that only the keyword is measured.
/// </summary>
[MemoryDiagnoser, ShortRunJob]
public class KeywordBoundaryBenchmarks
{
    private const string LiquidIdentifierPart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";

    private readonly Parser<string> _default = Literals.Keyword("in");
    private readonly Parser<string> _custom = Literals.Keyword("in", LiquidIdentifierPart);
    private readonly ParseContext _match = new(new Scanner("in (1..3)"));
    private readonly ParseContext _letter = new(new Scanner("inside"));
    private readonly ParseContext _underscore = new(new Scanner("in_x"));

    [Benchmark(Baseline = true)]
    public bool DefaultMatch() => Run(_default, _match);

    [Benchmark]
    public bool DefaultReject() => Run(_default, _letter);

    [Benchmark]
    public bool CustomMatch() => Run(_custom, _match);

    [Benchmark]
    public bool CustomReject() => Run(_custom, _underscore);

    private static bool Run(Parser<string> parser, ParseContext context)
    {
        context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<string>();
        return parser.Parse(context, ref result);
    }
}
