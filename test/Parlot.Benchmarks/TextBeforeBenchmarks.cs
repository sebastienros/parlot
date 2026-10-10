using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method      | Length | Mean      | Error     | StdDev    |
// |------------ |------- |----------:|----------:|----------:|
// | FalseStarts | 1000   |  5.894 us |  1.284 us | 0.0704 us |
// | FalseStarts | 4000   | 20.736 us | 23.139 us | 1.2683 us |
// | FalseStarts | 16000  | 77.909 us |  2.168 us | 0.1188 us |

// Measures AnyCharBefore when the delimiter can start with several chars. The text holds many false starts of the
// first delimiter and none of the second one, which is the worst case when each expected char is searched in turn:
// the frameworks without SearchValues did so, and read the whole text at each false start.
// This project only targets net10.0, which jumps with SearchValues.

[MemoryDiagnoser, ShortRunJob]
public class TextBeforeBenchmarks
{
    private readonly Parser<TextSpan> _parser = AnyCharBefore(Literals.Text("-->").Or(Literals.Text("==>")));

    private ParseContext _context;

    [Params(1_000, 4_000, 16_000)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _context = new ParseContext(new Scanner(CreateText(Length)));

        if (FalseStarts().Length != Length)
        {
            throw new InvalidOperationException("Incorrect text parsing.");
        }
    }

    internal static string CreateText(int length) => new string('-', length).Replace("--", "a-") + "-->";

    [Benchmark]
    public TextSpan FalseStarts()
    {
        var result = new ParseResult<TextSpan>();

        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _parser.Parse(_context, ref result);

        return result.Value;
    }
}
