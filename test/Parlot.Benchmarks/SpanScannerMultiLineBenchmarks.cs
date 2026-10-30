using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Parses tokens containing new lines with Terms.Scan: 100 block comments of 10 lines of 60 chars, and 1,000 tokens
// of two short lines. The cursor tracks the lines and columns of each token.

// BenchmarkDotNet v0.15.8, Windows 11 (10.0.28000.3365/26H1/2026Update)
// Intel Core Ultra X7 358H 1.90GHz, pinned to a performance core (--affinity 4)
//   DefaultJob : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3
//
// Before, the new lines were found with IndexOf, then counted by Advance char by char:
//
// | Method        | Mean     | Error    | StdDev   | Allocated |
// |-------------- |---------:|---------:|---------:|----------:|
// | BlockComments | 23.22 us | 0.032 us | 0.028 us |     176 B |
// | ShortLines    | 13.60 us | 0.024 us | 0.022 us |     176 B |
//
// After, the last new line is found with LastIndexOf and the others counted at once:
//
// | Method        | Mean      | Error     | StdDev    | Allocated |
// |-------------- |----------:|----------:|----------:|----------:|
// | BlockComments |  5.305 us | 0.0078 us | 0.0061 us |     176 B |
// | ShortLines    | 11.494 us | 0.0226 us | 0.0176 us |     176 B |
//
// Tokens without new lines (SpanScannerBenchmarks) are unchanged, within the noise.

[MemoryDiagnoser]
public class SpanScannerMultiLineBenchmarks
{
    private static readonly Parser<TextSpan> _blockComment = Terms.Scan(BlockComment, "/");
    private static readonly Parser<TextSpan> _shortLines = Terms.Scan(UpToSpace);

    private static readonly string _blockComments = string.Join(" ", Enumerable.Range(0, 100).Select(static i =>
        "/*" + string.Join("\n", Enumerable.Repeat("comment " + i.ToString("000") + " " + new string('-', 48), 10)) + "*/"));

    private static readonly string _shortLinesText = string.Join(" ", Enumerable.Range(0, 1000).Select(static i => "line" + i + "\nnext"));

    public SpanScannerMultiLineBenchmarks()
    {
        if (BlockComments() != 100) throw new InvalidOperationException(nameof(BlockComments));
        if (ShortLines() != 1000) throw new InvalidOperationException(nameof(ShortLines));
    }

    [Benchmark]
    public int BlockComments() => Count(_blockComment, _blockComments);

    [Benchmark]
    public int ShortLines() => Count(_shortLines, _shortLinesText);

    // A single context reads every token, the allocations are the context's
    private static int Count(Parser<TextSpan> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<TextSpan>();
        var count = 0;

        while (parser.Parse(context, ref result))
        {
            count++;
        }

        return count;
    }

    // The text is a string, so a comment that isn't closed at the end of the span is not one
    private static int BlockComment(ReadOnlySpan<char> span)
    {
        if (!span.StartsWith("/*"))
        {
            return 0;
        }

        var end = span.Slice(2).IndexOf("*/");

        if (end < 0)
        {
            return 0;
        }

        return end + 4;
    }

    private static int UpToSpace(ReadOnlySpan<char> span)
    {
        var index = span.IndexOf(' ');

        if (index >= 0)
        {
            return index;
        }

        return span.Length;
    }
}
