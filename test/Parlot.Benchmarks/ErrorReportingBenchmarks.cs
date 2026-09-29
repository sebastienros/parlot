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

    private static readonly Parser<char> _warningParser = Literals.Char('a').Warning("deprecated a");

    [Benchmark(Baseline = true)]
    public bool RuntimeDefault()
    {
        var context = new ParseContext(new Scanner("a"));
        return _parser.TryParse(context, out _, out _);
    }

    [Benchmark]
    public bool RuntimeCollectNoError()
    {
        var context = new ParseContext(new Scanner("a")) { CollectDiagnostics = true };
        return _parser.TryParse(context, out _, out _) && context.Diagnostics.Count == 0;
    }

    [Benchmark]
    public int RuntimeCollectError()
    {
        var context = new ParseContext(new Scanner("b")) { CollectDiagnostics = true };
        _parser.TryParse(context, out _, out _);
        return context.Diagnostics.Count;
    }

    [Benchmark]
    public bool RuntimeWarningIgnored()
    {
        var context = new ParseContext(new Scanner("a"));
        return _warningParser.TryParse(context, out _, out _);
    }

    [Benchmark]
    public int RuntimeWarningCollected()
    {
        var context = new ParseContext(new Scanner("a")) { CollectDiagnostics = true };
        _warningParser.TryParse(context, out _, out _);
        return context.Diagnostics.Count;
    }

    [Benchmark]
    public bool GeneratedDefault() => TryParse("a", out _);

    [Benchmark]
    public bool GeneratedCollectNoError() => TryParse("a", out _, out var diagnostics) && diagnostics.Count == 0;

    [Benchmark]
    public int GeneratedCollectError()
    {
        TryParse("b", out _, out var diagnostics);
        return diagnostics.Count;
    }

    private static partial bool TryParse(string input, out char value);
    private static partial bool TryParse(string input, out char value,
        out System.Collections.Generic.IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> diagnostics);
}
