using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method                 | Length | Mean      | Error     | StdDev    |
// |----------------------- |------- |----------:|----------:|----------:|
// | Ordinal_Match          | 1      |  5.634 ns | 0.3713 ns | 0.0204 ns |
// | Ordinal_Miss           | 1      |  2.462 ns | 0.1363 ns | 0.0075 ns |
// | IgnoreCase_Match       | 1      |  5.551 ns | 0.1057 ns | 0.0058 ns |
// | IgnoreCase_MatchedText | 1      |  6.767 ns | 0.4118 ns | 0.0226 ns |
// | Ordinal_Match          | 8      |  5.436 ns | 0.1783 ns | 0.0098 ns |
// | Ordinal_Miss           | 8      |  2.413 ns | 0.1969 ns | 0.0108 ns |
// | IgnoreCase_Match       | 8      |  9.655 ns | 0.2001 ns | 0.0110 ns |
// | IgnoreCase_MatchedText | 8      | 10.807 ns | 0.2194 ns | 0.0120 ns |
// | Ordinal_Match          | 32     |  5.863 ns | 0.0815 ns | 0.0045 ns |
// | Ordinal_Miss           | 32     |  2.910 ns | 0.2330 ns | 0.0128 ns |
// | IgnoreCase_Match       | 32     |  6.740 ns | 0.1880 ns | 0.0103 ns |
// | IgnoreCase_MatchedText | 32     |  8.562 ns | 0.1557 ns | 0.0085 ns |

// Measures TextLiteral.Parse on its own. The context is reused and the cursor moved back after each
// parse, such that neither the allocations of a parse nor the other parsers are part of the result.

[MemoryDiagnoser, ShortRunJob]
public class TextLiteralBenchmarks
{
    private Parser<string> _ordinal;
    private Parser<string> _ignoreCase;
    private Parser<string> _ignoreCaseMatchedText;
    private ParseContext _match;
    private ParseContext _miss;
    private TextPosition _start;

    [Params(1, 8, 32)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var text = new string('a', Length);

        _ordinal = Literals.Text(text);
        _ignoreCase = Literals.Text(text, caseInsensitive: true);
        _ignoreCaseMatchedText = Literals.Text(text, caseInsensitive: true, returnMatchedText: true);

        _match = new ParseContext(new Scanner(text + " tail"));
        _miss = new ParseContext(new Scanner(text.Substring(0, Length - 1) + "b tail"));
        _start = _match.Scanner.Cursor.Position;

        if (Parse(_ordinal, _match) != text
            || Parse(_ignoreCase, _match) != text
            || Parse(_ignoreCaseMatchedText, _match) != text
            || Parse(_ordinal, _miss) != null)
        {
            throw new InvalidOperationException("Incorrect text literal parsing.");
        }
    }

    [Benchmark(Baseline = true)]
    public string Ordinal_Match() => Parse(_ordinal, _match);

    [Benchmark]
    public string Ordinal_Miss() => Parse(_ordinal, _miss);

    [Benchmark]
    public string IgnoreCase_Match() => Parse(_ignoreCase, _match);

    [Benchmark]
    public string IgnoreCase_MatchedText() => Parse(_ignoreCaseMatchedText, _match);

    private string Parse(Parser<string> parser, ParseContext context)
    {
        var result = new ParseResult<string>();

        if (!parser.Parse(context, ref result))
        {
            return null;
        }

        context.Scanner.Cursor.ResetPosition(_start);
        return result.Value;
    }
}
