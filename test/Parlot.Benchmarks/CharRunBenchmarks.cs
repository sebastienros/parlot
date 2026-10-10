using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method                    | Length | Mean      | Error       | StdDev     |
// |-------------------------- |------- |----------:|------------:|-----------:|
// | Identifier                | 4      |  8.823 ns |   0.7169 ns |  0.0393 ns |
// | IdentifierExtraPredicates | 4      | 12.654 ns |   0.4387 ns |  0.0240 ns |
// | Pattern                   | 4      |  8.733 ns |   0.6544 ns |  0.0359 ns |
// | NonWhiteSpace             | 4      |  8.844 ns |   1.3723 ns |  0.0752 ns |
// | Identifier                | 16     | 14.909 ns |   1.6546 ns |  0.0907 ns |
// | IdentifierExtraPredicates | 16     | 14.814 ns |   1.6518 ns |  0.0905 ns |
// | Pattern                   | 16     | 13.291 ns |   0.4376 ns |  0.0240 ns |
// | NonWhiteSpace             | 16     | 14.787 ns |   1.8454 ns |  0.1012 ns |
// | Identifier                | 64     | 41.085 ns |   3.1233 ns |  0.1712 ns |
// | IdentifierExtraPredicates | 64     | 62.074 ns | 549.7639 ns | 30.1344 ns |
// | Pattern                   | 64     | 53.456 ns | 215.3463 ns | 11.8039 ns |
// | NonWhiteSpace             | 64     | 42.472 ns |  12.3673 ns |  0.6779 ns |

// Measures the parsers reading a run of chars with a predicate: Identifier (the one used with custom predicates,
// and on the frameworks without SearchValues), Pattern and NonWhiteSpace. The context is reused and the cursor
// moved back before each parse, such that the allocations of a parse are not part of the result.

[MemoryDiagnoser, ShortRunJob]
public class CharRunBenchmarks
{
    private readonly Parser<TextSpan> _identifier = new Identifier();
    private readonly Parser<TextSpan> _identifierExtra = new Identifier(static c => c == '@', static c => c == '-');
    private readonly Parser<TextSpan> _pattern = Literals.Pattern(static c => c != ' ');
    private readonly Parser<TextSpan> _nonWhiteSpace = Literals.NonWhiteSpace();

    private ParseContext _context;

    [Params(4, 16, 64)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _context = new ParseContext(new Scanner(new string('a', Length) + " = 123"));

        if (Identifier().Length != Length || IdentifierExtraPredicates().Length != Length
            || Pattern().Length != Length || NonWhiteSpace().Length != Length)
        {
            throw new InvalidOperationException("Incorrect char run parsing.");
        }
    }

    [Benchmark(Baseline = true)]
    public TextSpan Identifier() => Parse(_identifier);

    [Benchmark]
    public TextSpan IdentifierExtraPredicates() => Parse(_identifierExtra);

    [Benchmark]
    public TextSpan Pattern() => Parse(_pattern);

    [Benchmark]
    public TextSpan NonWhiteSpace() => Parse(_nonWhiteSpace);

    [Benchmark]
    public int ReadWhile()
    {
        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _context.Scanner.ReadWhile(static c => c != ' ', out var result);
        return result.Length;
    }

    private TextSpan Parse(Parser<TextSpan> parser)
    {
        var result = new ParseResult<TextSpan>();

        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        parser.Parse(_context, ref result);

        return result.Value;
    }
}
