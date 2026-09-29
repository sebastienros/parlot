using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public partial class ErrorReportingBenchmarks
{
    private static readonly Parser<char> _parser = OneOf(
        Literals.Char('a'),
        Literals.Char('b').Error("unexpected b"));

    [Benchmark(Baseline = true)]
    public bool RuntimeDefault()
    {
        var context = new ParseContext(new Scanner("a"));
        return _parser.TryParse(context, out _, out _);
    }

    [Benchmark]
    public bool RuntimeCollectNoError()
    {
        var context = new ParseContext(new Scanner("a")) { ContinueOnError = true };
        return _parser.TryParse(context, out _, out _) && context.Errors.Count == 0;
    }

    [Benchmark]
    public int RuntimeCollectError()
    {
        var context = new ParseContext(new Scanner("b")) { ContinueOnError = true };
        _parser.TryParse(context, out _, out _);
        return context.Errors.Count;
    }

    [Benchmark]
    public bool GeneratedDefault() => TryParse("a", out _);

    [Benchmark]
    public bool GeneratedCollectNoError() => TryParse("a", out _, out var errors) && errors.Count == 0;

    [Benchmark]
    public int GeneratedCollectError()
    {
        TryParse("b", out _, out var errors);
        return errors.Count;
    }

    private static partial bool TryParse(string input, out char value);
    private static partial bool TryParse(string input, out char value,
        out System.Collections.Generic.IReadOnlyList<(string Message, int Offset, int Line, int Column)> errors);
}
