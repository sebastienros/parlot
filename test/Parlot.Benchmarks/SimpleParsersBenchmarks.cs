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
        GeneratedParsers.TryParseText(TextInput, out var result);
        return result;
    }

    private static readonly Parser<string> _matchedTextFluent =
        Literals.Text("hello", caseInsensitive: true, returnMatchedText: true);

    private const string ExactTextInput = "hello";
    private const string DifferentCaseTextInput = "HELLO";
    private const string SlicedTextInput = "HELLO world";

    [Benchmark(Baseline = true), BenchmarkCategory("MatchedTextExact")]
    public string MatchedTextExact_Fluent()
    {
        _matchedTextFluent.TryParse(ExactTextInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("MatchedTextExact")]
    public string MatchedTextExact_Generated()
    {
        GeneratedParsers.TryParseMatchedText(ExactTextInput, out var result);
        return result;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("MatchedTextDifferentCase")]
    public string MatchedTextDifferentCase_Fluent()
    {
        _matchedTextFluent.TryParse(DifferentCaseTextInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("MatchedTextDifferentCase")]
    public string MatchedTextDifferentCase_Generated()
    {
        GeneratedParsers.TryParseMatchedText(DifferentCaseTextInput, out var result);
        return result;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("MatchedTextSliced")]
    public string MatchedTextSliced_Fluent()
    {
        _matchedTextFluent.TryParse(SlicedTextInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("MatchedTextSliced")]
    public string MatchedTextSliced_Generated()
    {
        GeneratedParsers.TryParseMatchedText(SlicedTextInput, out var result);
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
        GeneratedParsers.TryParseDecimal(DecimalInput, out var result);
        return result;
    }

    // ==================== Integer ====================

    private static readonly Parser<long> _integerFluent = Terms.Integer();

    private const string IntegerInput = "123";

    [Benchmark(Baseline = true), BenchmarkCategory("Integer")]
    public long Integer_Fluent()
    {
        _integerFluent.TryParse(IntegerInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("Integer")]
    public long Integer_Generated()
    {
        GeneratedParsers.TryParseInteger(IntegerInput, out var result);
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
        GeneratedParsers.TryParseOneOf(OneOfInput, out var result);
        return result;
    }

    // ==================== OneOf without whitespace ====================

    private static readonly Parser<string> _literalOneOfFluent = OneOf(Literals.Text("apple"), Literals.Text("banana"), Literals.Text("cherry"));

    [Benchmark(Baseline = true), BenchmarkCategory("LiteralOneOf")]
    public string LiteralOneOf_Fluent()
    {
        _literalOneOfFluent.TryParse(OneOfInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("LiteralOneOf")]
    public string LiteralOneOf_Generated()
    {
        GeneratedParsers.TryParseLiteralOneOf(OneOfInput, out var result);
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
        GeneratedParsers.TryParseAnd(AndInput, out var result);
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
        GeneratedParsers.TryParseZeroOrMany(ZeroOrManyInput, out var result);
        return result;
    }

    // ==================== ZeroOrOne ====================

    private static readonly Parser<IReadOnlyList<decimal>> _zeroOrOneFluent = ZeroOrOne(Terms.Decimal());

    [Params("123", "word")]
    public string ZeroOrOneInput { get; set; } = "123";

    [Benchmark(Baseline = true), BenchmarkCategory("ZeroOrOne")]
    public IReadOnlyList<decimal> ZeroOrOne_Fluent()
    {
        _zeroOrOneFluent.TryParse(ZeroOrOneInput, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("ZeroOrOne")]
    public IReadOnlyList<decimal> ZeroOrOne_Generated()
    {
        GeneratedParsers.TryParseZeroOrOne(ZeroOrOneInput, out var result);
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
        GeneratedParsers.TryParseSkipWhiteSpace(SkipWhiteSpaceInput, out var result);
        return result;
    }
}
