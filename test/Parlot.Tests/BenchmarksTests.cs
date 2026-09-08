#if NET10_0_OR_GREATER
using Parlot.Benchmarks;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Parlot.Tests;

public class BenchmarksTests
{
    const decimal _expected1 = (decimal)3.5;
    const decimal _expected2 = (decimal)-64.5;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationParsing(bool canBeCanceled)
    {
        var benchmarks = new CancellationBenchmarks { CanBeCanceled = canBeCanceled };
        benchmarks.Setup();
        try
        {
            Assert.Equal(128, benchmarks.Fluent());
            Assert.Equal(128, benchmarks.Generated());
            Assert.Equal(128, benchmarks.GeneratedWithoutToken());
        }
        finally
        {
            benchmarks.Cleanup();
        }
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("\u00e9", true)]
    [InlineData("\u4e2d", false)]
    public void CharacterMapLookup(string input, bool success)
    {
        var benchmarks = new CharMapBenchmarks { Input = input };
        benchmarks.Setup();

        Assert.Equal(success, benchmarks.Lookup());
        Assert.Equal(success, benchmarks.Lookup());
    }

    [Fact]
    public void CursorMatchHello()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.CursorMatchHello();
        Assert.NotNull(result);
    }

    [Fact]
    public void CursorMatchGoodbye()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.CursorMatchGoodbye();
        Assert.NotNull(result);
    }

    [Fact]
    public void CursorMatchNone()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.CursorMatchNone();
        Assert.Null(result);
    }

    [Fact]
    public void Lookup()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.Lookup();
        Assert.Equal('d', result);
    }

    [Fact]
    public void SkipWhiteSpace_1()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.SkipWhiteSpace_1();
        Assert.Equal('a', result);
    }

    [Fact]
    public void SkipWhiteSpace_10()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.SkipWhiteSpace_10();
        Assert.Equal('a', result);
    }

    [Fact]
    public void DecodeStringWithoutEscapes()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.DecodeStringWithoutEscapes();
        Assert.Equal("This is a new line \n \t and a tab and some \xa0", result);
    }

    [Fact]
    public void DecodeStringWithEscapes()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.DecodeStringWithEscapes();
        Assert.Equal("This is a new line \n \t and a tab and some \xa0", result);
    }

    [Fact]
    public void ExpressionRawSmall()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotRawSmall();
        Assert.NotNull(result);
        Assert.Equal(_expected1, result.Evaluate());
    }

    [Fact]
    public void ExpressionFluentSmall()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotFluentSmall();
        Assert.NotNull(result);
        Assert.Equal(_expected1, result.Evaluate());
    }

    [Fact]
    public void ExpressionGeneratedSmall()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotGeneratedSmall();
        Assert.NotNull(result);
        Assert.Equal(_expected1, result.Evaluate());
    }

    [Fact]
    public void ExpressionRawBig()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotRawBig();
        Assert.NotNull(result);
        Assert.Equal(_expected2, result.Evaluate());
    }

    [Fact]
    public void ExpressionFluentBig()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotFluentBig();
        Assert.NotNull(result);
        Assert.Equal(_expected2, result.Evaluate());
    }

    [Fact]
    public void ExpressionGeneratedBig()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotGeneratedBig();
        Assert.NotNull(result);
        Assert.Equal(_expected2, result.Evaluate());
    }

    [Fact]
    public void ExpressionFluentUnary()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotFluentUnary();
        Assert.NotNull(result);
        Assert.Equal(26m, result.Evaluate());
    }

    [Fact]
    public void ExpressionGeneratedUnary()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotGeneratedUnary();
        Assert.NotNull(result);
        Assert.Equal(26m, result.Evaluate());
    }

    [Fact]
    public void BigJson()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.BigJson_Parlot();
        Assert.NotNull(result);
    }

    [Fact]
    public void BigJsonGenerated()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.BigJson_ParlotGenerated();
        Assert.NotNull(result);
    }

    [Fact]
    public void DeepJson()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.DeepJson_Parlot();
        Assert.NotNull(result);
    }

    [Fact]
    public void DeepJsonGenerated()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.DeepJson_ParlotGenerated();
        Assert.NotNull(result);
    }

    [Fact]
    public void LongJson()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.LongJson_Parlot();
        Assert.NotNull(result);
    }

    [Fact]
    public void LongJsonGenerated()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.LongJson_ParlotGenerated();
        Assert.NotNull(result);
    }

    [Fact]
    public void WideJson()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        benchmarks.WideJson_Parlot();
    }

    [Fact]
    public void WideJsonGenerated()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.WideJson_ParlotGenerated();
        Assert.NotNull(result);
    }

    [Fact]
    public void ParlotEmail()
    {
        var benchmarks = new RegexBenchmarks();
        var result = benchmarks.ParlotEmail();
        Assert.Equal(RegexBenchmarks.Email, result);
    }

    [Fact]
    public void ParlotEmailGenerated()
    {
        var benchmarks = new RegexBenchmarks();
        var result = benchmarks.ParlotEmailGenerated();
        Assert.Equal(RegexBenchmarks.Email, result);
    }

    [Fact]
    public void SimpleGeneratedParsers()
    {
        var benchmarks = new SimpleParsersBenchmarks();

        Assert.Equal("hello", benchmarks.Text_Generated());
        Assert.Equal(123.456m, benchmarks.Decimal_Generated());
        Assert.Equal(123L, benchmarks.Integer_Generated());
        Assert.Equal("cherry", benchmarks.OneOf_Generated());
        Assert.Equal("cherry", benchmarks.LiteralOneOf_Generated());
        Assert.Equal(("price", 99.99m), benchmarks.And_Generated());
        Assert.Equal(new[] { 1m, 2m, 3m, 4m, 5m }, benchmarks.ZeroOrMany_Generated());
        Assert.Equal(42.5m, benchmarks.SkipWhiteSpace_Generated());
    }

    [Fact]
    public void GeneratedCollections()
    {
        foreach (var combinator in new[] { "ZeroOrMany", "OneOrMany", "Separated" })
        {
            var benchmarks = new CollectionBenchmarks { Count = 4, Combinator = combinator };
            benchmarks.Setup();
            Assert.True(benchmarks.Generated());
        }
    }

    [Fact]
    public void GeneratedConfiguration()
    {
        var benchmarks = new IfSelectBenchmarks { Condition = true, Match = true };
        benchmarks.Setup();

        Assert.True(benchmarks.IfGenerated());
        Assert.True(benchmarks.SelectGenerated());
        Assert.True(benchmarks.IfElseGenerated());
        Assert.True(benchmarks.SelectElseGenerated());
    }

    [Fact]
    public void GeneratedSql()
    {
        var benchmarks = new SqlParserBenchmarks
        {
            Sql = "select a where a not like '%foo%'"
        };

        Assert.True(benchmarks.Generated());
    }

    [Fact]
    public void ParlotLookupFluent()
    {
        var benchmarks = new SwitchExpressionBenchmarks() { Length = 2 };
        benchmarks.Setup();
        var result = benchmarks.LookupMatchFluent();
    }

}
#endif
