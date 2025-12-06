using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Run with: dotnet run -f net10.0 -c Release -- --filter "*SimpleParsersBenchmarks*" --job short --inProcess
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SimpleParsersBenchmarks
{
    // ==================== Text ====================
    
    private static readonly Parser<string> _textFluent = Terms.Text("hello");

    private const string TextInput = "hello world";

    [Benchmark(Baseline = true), BenchmarkCategory("Text")]
    public string Text_Fluent()
    {
        _textFluent.TryParse(TextInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("Text")]
    public string Text_Generated()
    {
        GeneratedParsers.TextParser.TryParse(TextInput, out var result);
        return result;
    }

    // ==================== Decimal ====================

    private static readonly Parser<decimal> _decimalFluent = Terms.Decimal();

    private const string DecimalInput = "123.456";

    [Benchmark(Baseline = true), BenchmarkCategory("Decimal")]
    public decimal Decimal_Fluent()
    {
        _decimalFluent.TryParse(DecimalInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("Decimal")]
    public decimal Decimal_Generated()
    {
        GeneratedParsers.DecimalParser.TryParse(DecimalInput, out var result);
        return result;
    }

    // ==================== OneOf ====================

    private static readonly Parser<string> _oneOfFluent = OneOf(Terms.Text("apple"), Terms.Text("banana"), Terms.Text("cherry"));

    private const string OneOfInput = "cherry pie";

    [Benchmark(Baseline = true), BenchmarkCategory("OneOf")]
    public string OneOf_Fluent()
    {
        _oneOfFluent.TryParse(OneOfInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("OneOf")]
    public string OneOf_Generated()
    {
        GeneratedParsers.OneOfParser.TryParse(OneOfInput, out var result);
        return result;
    }

    // ==================== And ====================

    private static readonly Parser<(string, decimal)> _andFluent = Terms.Text("price").And(Terms.Decimal());

    private const string AndInput = "price 99.99";

    [Benchmark(Baseline = true), BenchmarkCategory("And")]
    public (string, decimal) And_Fluent()
    {
        _andFluent.TryParse(AndInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("And")]
    public (string, decimal) And_Generated()
    {
        GeneratedParsers.AndParser.TryParse(AndInput, out var result);
        return result;
    }

    // ==================== ZeroOrMany ====================

    private static readonly Parser<IReadOnlyList<decimal>> _zeroOrManyFluent = ZeroOrMany(Terms.Decimal());

    private const string ZeroOrManyInput = "1 2 3 4 5";

    [Benchmark(Baseline = true), BenchmarkCategory("ZeroOrMany")]
    public IReadOnlyList<decimal> ZeroOrMany_Fluent()
    {
        _zeroOrManyFluent.TryParse(ZeroOrManyInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("ZeroOrMany")]
    public IReadOnlyList<decimal> ZeroOrMany_Generated()
    {
        GeneratedParsers.ZeroOrManyParser.TryParse(ZeroOrManyInput, out var result);
        return result;
    }

    // ==================== SkipWhiteSpace ====================

    private static readonly Parser<decimal> _skipWhiteSpaceFluent = SkipWhiteSpace(Literals.Decimal());

    private const string SkipWhiteSpaceInput = "   42.5";

    [Benchmark(Baseline = true), BenchmarkCategory("SkipWhiteSpace")]
    public decimal SkipWhiteSpace_Fluent()
    {
        _skipWhiteSpaceFluent.TryParse(SkipWhiteSpaceInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("SkipWhiteSpace")]
    public decimal SkipWhiteSpace_Generated()
    {
        GeneratedParsers.SkipWhiteSpaceParser.TryParse(SkipWhiteSpaceInput, out var result);
        return result;
    }
}
