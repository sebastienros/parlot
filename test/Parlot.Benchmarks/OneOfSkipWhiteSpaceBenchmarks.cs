using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method                             | WhiteSpace | Mean      | Error      | StdDev    |
// |----------------------------------- |----------- |----------:|-----------:|----------:|
// | DistinctFirstChar                  |            |  8.349 ns |  4.1441 ns | 0.2272 ns |
// | SharedFirstChar                    |            | 19.252 ns |  3.7110 ns | 0.2034 ns |
// | DistinctFirstChar_WhiteSpaceParser |            | 11.519 ns |  1.2559 ns | 0.0688 ns |
// | SharedFirstChar_WhiteSpaceParser   |            | 18.817 ns |  0.7648 ns | 0.0419 ns |
// | DistinctFirstChar                  | (3 spaces) | 10.191 ns |  2.1427 ns | 0.1175 ns |
// | SharedFirstChar                    | (3 spaces) | 20.844 ns | 14.1803 ns | 0.7773 ns |
// | DistinctFirstChar_WhiteSpaceParser | (3 spaces) | 10.500 ns |  5.7873 ns | 0.3172 ns |
// | SharedFirstChar_WhiteSpaceParser   | (3 spaces) | 24.589 ns | 31.7934 ns | 1.7427 ns |

// Measures OneOf.Parse when every alternative skips white spaces, in which case the white spaces are skipped once
// by OneOf and the alternatives are found in its lookup map. The contexts are reused and the cursor moved back
// before each parse, such that the allocations of a parse are not part of the result.

[MemoryDiagnoser, ShortRunJob]
public class OneOfSkipWhiteSpaceBenchmarks
{
    private readonly Parser<string> _distinct = OneOf(
        Terms.Text("apple"), Terms.Text("banana"), Terms.Text("cherry"), Terms.Text("damson"));

    // The alternatives share their first char, so the last one is reached after the others failed
    private readonly Parser<string> _sharedFirstChar = OneOf(
        Terms.Text("alpha"), Terms.Text("apple"), Terms.Text("apricot"), Terms.Text("avocado"));

    private ParseContext _distinctContext;
    private ParseContext _sharedContext;
    private ParseContext _distinctWhiteSpaceContext;
    private ParseContext _sharedWhiteSpaceContext;

    [Params("", "   ")]
    public string WhiteSpace { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _distinctContext = new ParseContext(new Scanner(WhiteSpace + "damson"));
        _sharedContext = new ParseContext(new Scanner(WhiteSpace + "avocado"));

        // A custom white space parser disables the shortcut of SkipWhiteSpace
        _distinctWhiteSpaceContext = new ParseContext(new Scanner(WhiteSpace + "damson"))
        {
            WhiteSpaceParser = Literals.WhiteSpace(includeNewLines: true)
        };

        _sharedWhiteSpaceContext = new ParseContext(new Scanner(WhiteSpace + "avocado"))
        {
            WhiteSpaceParser = Literals.WhiteSpace(includeNewLines: true)
        };

        if (DistinctFirstChar() != "damson" || SharedFirstChar() != "avocado"
            || DistinctFirstChar_WhiteSpaceParser() != "damson" || SharedFirstChar_WhiteSpaceParser() != "avocado")
        {
            throw new InvalidOperationException("Incorrect OneOf parsing.");
        }
    }

    [Benchmark(Baseline = true)]
    public string DistinctFirstChar() => Parse(_distinct, _distinctContext);

    [Benchmark]
    public string SharedFirstChar() => Parse(_sharedFirstChar, _sharedContext);

    [Benchmark]
    public string DistinctFirstChar_WhiteSpaceParser() => Parse(_distinct, _distinctWhiteSpaceContext);

    [Benchmark]
    public string SharedFirstChar_WhiteSpaceParser() => Parse(_sharedFirstChar, _sharedWhiteSpaceContext);

    private static string Parse(Parser<string> parser, ParseContext context)
    {
        var result = new ParseResult<string>();

        context.Scanner.Cursor.ResetPosition(TextPosition.Start);

        if (!parser.Parse(context, ref result))
        {
            return null;
        }

        return result.Value;
    }
}
