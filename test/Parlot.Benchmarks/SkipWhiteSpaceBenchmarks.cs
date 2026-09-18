using BenchmarkDotNet.Attributes;
using System;

namespace Parlot.Benchmarks;

// | Method                                   | Length | Mean      | Error     | StdDev    | Gen0   | Allocated |
// |----------------------------------------- |------- |----------:|----------:|----------:|-------:|----------:|
// | SkipWhiteSpace_Default                   | 0      |  7.778 ns |   3.720 ns | 0.2039 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLine_Default          | 0      |  8.208 ns |   3.298 ns | 0.1808 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_Default                   | 1      |  8.469 ns |   5.884 ns | 0.3225 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLine_Default          | 1      | 10.230 ns |   3.512 ns | 0.1925 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_Default                   | 2      | 10.012 ns |   7.336 ns | 0.4021 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLine_Default          | 2      | 12.287 ns |   4.735 ns | 0.2595 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_Default                   | 10     | 15.330 ns |   5.858 ns | 0.3211 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLine_Default          | 10     | 24.955 ns |   2.370 ns | 0.1299 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_Vectorized                | 0      |  5.915 ns |   2.689 ns | 0.1474 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_Vectorized      | 0      |  6.623 ns |   5.508 ns | 0.3019 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_Vectorized                | 1      | 11.387 ns |  22.750 ns | 1.2470 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_Vectorized      | 1      | 11.339 ns |   7.791 ns | 0.4270 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_Vectorized                | 2      | 12.038 ns |   5.787 ns | 0.3172 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_Vectorized      | 2      | 13.174 ns |   5.875 ns | 0.3220 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_Vectorized                | 10     |  9.651 ns |   5.120 ns | 0.2807 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_Vectorized      | 10     | 22.209 ns |  13.731 ns | 0.7527 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_PeekSearchValue           | 0      |  7.707 ns |   6.190 ns | 0.3393 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_PeekSearchValue | 0      |  7.718 ns |   9.743 ns | 0.5341 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_PeekSearchValue           | 1      |  9.911 ns |   7.295 ns | 0.3998 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_PeekSearchValue | 1      |  9.972 ns |   1.626 ns | 0.0891 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_PeekSearchValue           | 2      | 11.330 ns |   2.612 ns | 0.1432 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_PeekSearchValue | 2      | 12.462 ns |   6.301 ns | 0.3454 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_PeekSearchValue           | 10     | 28.590 ns |  11.779 ns | 0.6456 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_PeekSearchValue | 10     | 39.394 ns |  12.996 ns | 0.7124 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_PeekCharacter             | 0      |  6.187 ns |  1.2066 ns | 0.0661 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_PeekCharacter   | 0      |  6.038 ns |  1.1867 ns | 0.0650 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_PeekCharacter             | 1      |  7.856 ns |  0.9430 ns | 0.0517 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_PeekCharacter   | 1      |  9.136 ns | 11.7522 ns | 0.6442 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_PeekCharacter             | 2      |  7.682 ns |  2.3857 ns | 0.1308 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_PeekCharacter   | 2      |  8.943 ns |  2.7100 ns | 0.1485 ns | 0.0085 |      80 B |
// | SkipWhiteSpace_PeekCharacter             | 10     | 11.080 ns |  4.4907 ns | 0.2462 ns | 0.0085 |      80 B |
// | SkipWhiteSpaceOrNewLines_PeekCharacter   | 10     | 21.759 ns |  4.9365 ns | 0.2706 ns | 0.0085 |      80 B |

// This benchmark shows the performance of skipping white spaces in a string.
// For small sets of spaces (0,1) all implementation are very close in performance. For larger spans (10) the vectorized implementation is the fastest.

[MemoryDiagnoser, ShortRunJob]
public class SkipWhiteSpaceBenchmarks
{
    private string _source;
    private Scanner _scanner;

    [Params(0, 1, 2, 10)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _source = new string(' ', Length) + "a";
        _scanner = new Scanner(_source);
    }

    [Benchmark, BenchmarkCategory("SkipWhiteSpace")]
    public bool SkipWhiteSpace_Default()
    {
        return _scanner.SkipWhiteSpace();
    }

    [Benchmark, BenchmarkCategory("SkipWhiteSpaceNewLines")]
    public bool SkipWhiteSpaceOrNewLine_Default()
    {
        return _scanner.SkipWhiteSpaceOrNewLine();
    }

#if NET8_0_OR_GREATER
    [Benchmark, BenchmarkCategory("SkipWhiteSpace")]
    public bool SkipWhiteSpace_Vectorized()
    {
        var cursor = _scanner.Cursor;
        var span = cursor.Span;

        var index = span.IndexOfAnyExcept(SearchValuesHelper._whiteSpaces);

        // Only spaces ?
        // Not tracking new lines since we know these are only spaces
        switch (index)
        {
            case 0:
                return false;
            case -1:
                cursor.AdvanceNoNewLines(span.Length);
                return true;
            default:
                cursor.AdvanceNoNewLines(index);
                return true;
        }
    }

    [Benchmark, BenchmarkCategory("SkipWhiteSpaceNewLines")]
    public bool SkipWhiteSpaceOrNewLines_Vectorized()
    {
        var cursor = _scanner.Cursor;
        var span = cursor.Span;

        var index = span.IndexOfAnyExcept(SearchValuesHelper._whiteSpaceOrNewLines);

        // Only spaces ?
        switch (index)
        {
            case 0:
                return false;
            case -1:
                cursor.Advance(span.Length);
                return true;
            default:
                cursor.Advance(index);
                return true;
        }
    }

    [Benchmark, BenchmarkCategory("SkipWhiteSpace")]
    public bool SkipWhiteSpace_Vectorized_Optimized()
    {
        var cursor = _scanner.Cursor;
        var span = cursor.Span;

        // Check ASCII first
        var index = span.IndexOfAnyExcept(SearchValuesHelper._whiteSpacesAscii);

        // If we found a non-ASCII character, we need to check the full set
        if (index > 0 && index < span.Length && span[index] > 127)
        {
            index = span.Slice(index).IndexOfAnyExcept(SearchValuesHelper._whiteSpaces);
        }
        
        // Only spaces ?
        // Not tracking new lines since we know these are only spaces
        switch (index)
        {
            case 0:
                return false;
            case -1:
                cursor.AdvanceNoNewLines(span.Length);
                return true;
            default:
                cursor.AdvanceNoNewLines(index);
                return true;
        }
    }
#endif
}
