using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method  | Length | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
// |-------- |------- |----------:|----------:|----------:|-------:|-------:|----------:|
// | Plain   | 8      |  8.448 ns | 0.0192 ns | 0.0180 ns |      - |      - |         - |
// | Escaped | 8      | 28.756 ns | 0.2967 ns | 0.2776 ns | 0.0042 |      - |      40 B |
// | Plain   | 64     | 13.176 ns | 0.0814 ns | 0.0722 ns |      - |      - |         - |
// | Escaped | 64     | 55.839 ns | 0.0860 ns | 0.0804 ns | 0.0162 | 0.0001 |     152 B |
// | Plain   | 512    | 27.168 ns | 0.1048 ns | 0.0818 ns |      - |      - |         - |
// | Escaped | 512    | 97.058 ns | 1.3627 ns | 2.9040 ns | 0.1111 | 0.0067 |    1048 B |

// Measures StringLiteral on strings with and without escape sequences.

[MemoryDiagnoser, ShortRunJob]
public class StringLiteralBenchmarks
{
    private readonly Parser<TextSpan> _parser = Literals.String();

    private ParseContext _plainContext;
    private ParseContext _escapedContext;

    [Params(8, 64, 512)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _plainContext = new ParseContext(new Scanner($"\"{new string('a', Length)}\""));
        _escapedContext = new ParseContext(new Scanner($"\"{new string('a', Length - 2)}\\n\""));

        if (Plain().Length != Length || Escaped().Length != Length - 1)
        {
            throw new InvalidOperationException("Incorrect string parsing.");
        }
    }

    [Benchmark]
    public TextSpan Plain() => Parse(_plainContext);

    [Benchmark]
    public TextSpan Escaped() => Parse(_escapedContext);

    private TextSpan Parse(ParseContext context)
    {
        var result = new ParseResult<TextSpan>();

        context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _parser.Parse(context, ref result);

        return result.Value;
    }
}
