using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using Parlot.Tests;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Parses 1,000 Liquid identifiers (Fluid's IdentifierParser rules) separated by spaces with a Parser<TextSpan>
// subclass written like Fluid's (advancing the cursor one char at a time), a subclass calling the span scanner
// directly, and Terms.Scan with the span scanner.

// BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.2 (24H232) [Darwin 24.6.0]
// Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
//   DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
//
// | Method           | Mean      | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
// |----------------- |----------:|----------:|----------:|------:|-------:|----------:|------------:|
// | HandWritten      | 18.212 us | 0.0714 us | 0.0557 us |  1.00 |      - |     176 B |        1.00 |
// | HandWrittenSpan  |  7.789 us | 0.0499 us | 0.0417 us |  0.43 | 0.0153 |     176 B |        1.00 |
// | Scan             | 11.439 us | 0.1958 us | 0.1529 us |  0.63 | 0.0153 |     176 B |        1.00 |
// | ScanStaticLambda |  9.579 us | 0.1890 us | 0.1675 us |  0.53 |      - |     176 B |        1.00 |
//
// Compared with HandWrittenSpan, Scan pays about 1 ns per token for the check for new lines in the token,
// and 1 ns for the thunk of a delegate to a static method, which a static lambda avoids.

[MemoryDiagnoser]
public class SpanScannerBenchmarks
{
    private static readonly Parser<TextSpan> _handWritten = SkipWhiteSpace(new HandWrittenLiquidIdentifier());
    private static readonly Parser<TextSpan> _handWrittenSpan = SkipWhiteSpace(new SpanIdentifier());
    private static readonly Parser<TextSpan> _scan = Terms.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars);

    // A delegate to a static method goes through a thunk, unlike a static lambda
    private static readonly Parser<TextSpan> _scanStaticLambda = Terms.Scan(static span => LiquidScanners.Identifier(span), LiquidScanners.StartChars);

    private static readonly string _text = string.Join(" ", Enumerable.Range(0, 1000).Select(static i => (i % 4) switch
    {
        0 => "product",
        1 => "line_item" + i,
        2 => "page-title",
        _ => i + "th",
    }));

    public SpanScannerBenchmarks()
    {
        if (HandWritten() != 1000) throw new InvalidOperationException(nameof(HandWritten));
        if (HandWrittenSpan() != 1000) throw new InvalidOperationException(nameof(HandWrittenSpan));
        if (Scan() != 1000) throw new InvalidOperationException(nameof(Scan));
        if (ScanStaticLambda() != 1000) throw new InvalidOperationException(nameof(ScanStaticLambda));
    }

    [Benchmark(Baseline = true)]
    public int HandWritten() => Count(_handWritten);

    [Benchmark]
    public int HandWrittenSpan() => Count(_handWrittenSpan);

    [Benchmark]
    public int Scan() => Count(_scan);

    [Benchmark]
    public int ScanStaticLambda() => Count(_scanStaticLambda);

    // A single context reads every identifier, the allocations are the context's
    private static int Count(Parser<TextSpan> parser)
    {
        var context = new ParseContext(new Scanner(_text));
        var result = new ParseResult<TextSpan>();
        var count = 0;

        while (parser.Parse(context, ref result))
        {
            count++;
        }

        return count;
    }

    // A subclass calling the scanner directly instead of through a delegate
    private sealed class SpanIdentifier : Parser<TextSpan>
    {
        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            var cursor = context.Scanner.Cursor;
            var span = cursor.Span;
            var size = LiquidScanners.Identifier(span);

            if (size <= 0)
            {
                context.ExitParser(this);
                return false;
            }

            var start = cursor.Offset;
            cursor.AdvanceNoNewLines(size);
            result.Set(start, start + size, cursor.CreateSpan(start, size));

            context.ExitParser(this);
            return true;
        }
    }
}
