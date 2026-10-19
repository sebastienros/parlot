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
// The sparse text holds no expected char before the delimiter, like a comment: the jump reads it once.

[MemoryDiagnoser, ShortRunJob]
public partial class TextBeforeBenchmarks
{
    private readonly Parser<TextSpan> _parser = AnyCharBefore(Literals.Text("-->").Or(Literals.Text("==>")));

    private ParseContext _context;
    private ParseContext _sparseContext;
    private string _text;
    private string _sparseText;

    [Params(1_000, 4_000, 16_000)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _text = CreateText(Length);
        _context = new ParseContext(new Scanner(_text));

        if (FalseStarts().Length != Length)
        {
            throw new InvalidOperationException("Incorrect text parsing.");
        }

        if (FalseStartsGenerated() != Length)
        {
            throw new InvalidOperationException("Incorrect generated text parsing.");
        }

        _sparseText = CreateSparseText(Length);
        _sparseContext = new ParseContext(new Scanner(_sparseText));

        if (Sparse().Length != Length || SparseGenerated() != Length)
        {
            throw new InvalidOperationException("Incorrect sparse text parsing.");
        }
    }

    internal static string CreateText(int length) => new string('-', length).Replace("--", "a-") + "-->";

    internal static string CreateSparseText(int length) => new string('a', length) + "-->";

    [Benchmark]
    public TextSpan FalseStarts()
    {
        var result = new ParseResult<TextSpan>();

        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _parser.Parse(_context, ref result);

        return result.Value;
    }

    [Benchmark]
    public int FalseStartsGenerated()
    {
        _ = TryParseFalseStarts(_text, out var length);

        return length;
    }

    [Benchmark]
    public TextSpan Sparse()
    {
        var result = new ParseResult<TextSpan>();

        _sparseContext.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _parser.Parse(_sparseContext, ref result);

        return result.Value;
    }

    [Benchmark]
    public int SparseGenerated()
    {
        _ = TryParseFalseStarts(_sparseText, out var length);

        return length;
    }

    private static partial bool TryParseFalseStarts(string input, out int value);
}
