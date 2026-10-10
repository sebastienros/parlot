using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class ScannerDecimalFailureBenchmarks
{
    private Scanner _scanner;

    [GlobalSetup]
    public void Setup() => _scanner = new Scanner("-.x");

    [Benchmark]
    public bool SignedDecimalFailure()
    {
        _scanner.Cursor.ResetPosition(TextPosition.Start);
        return _scanner.ReadDecimal(NumberOptions.Any, out _);
    }
}
