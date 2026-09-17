using BenchmarkDotNet.Attributes;
using System;

namespace Parlot.Benchmarks;

// Reset before every invocation: otherwise repeated invocations measure an exhausted scanner.
[MemoryDiagnoser, ShortRunJob]
public class SkipWhiteSpaceBenchmarks
{
    private string _source;
    private Scanner _scanner;

    [Params(0, 1, 2, 10, 256)]
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
        _scanner.Cursor.ResetPosition(TextPosition.Start);
        return _scanner.SkipWhiteSpace();
    }

    [Benchmark, BenchmarkCategory("SkipWhiteSpaceNewLines")]
    public bool SkipWhiteSpaceOrNewLine_Default()
    {
        _scanner.Cursor.ResetPosition(TextPosition.Start);
        return _scanner.SkipWhiteSpaceOrNewLine();
    }

#if NET8_0_OR_GREATER
    [Benchmark, BenchmarkCategory("SkipWhiteSpace")]
    public bool SkipWhiteSpace_Vectorized()
    {
        _scanner.Cursor.ResetPosition(TextPosition.Start);
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
        _scanner.Cursor.ResetPosition(TextPosition.Start);
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
        _scanner.Cursor.ResetPosition(TextPosition.Start);
        var cursor = _scanner.Cursor;
        var span = cursor.Span;

        // Check ASCII first
        var index = span.IndexOfAnyExcept(SearchValuesHelper._whiteSpacesAscii);

        // If we found a non-ASCII character, we need to check the full set
        if (index >= 0 && span[index] > 127)
        {
            var remainingIndex = span.Slice(index).IndexOfAnyExcept(SearchValuesHelper._whiteSpaces);
            index = remainingIndex < 0 ? -1 : index + remainingIndex;
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
