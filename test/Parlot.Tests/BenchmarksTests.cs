#if NET10_0_OR_GREATER
using Parlot.Benchmarks;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Parlot.Tests;

public class BenchmarksTests
{
    const decimal _expected1 = (decimal)3.5;
    const decimal _expected2 = (decimal)-64.5;

    [Fact]
    public void CreateCompiledSmallParser()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.CreateCompiledSmallParser();
        Assert.NotNull(result);
    }

    [Fact]
    public void CreateCompiledExpressionParser()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.CreateCompiledExpressionParser();
        Assert.NotNull(result);
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
    public void ExpressionCompiledSmall()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotCompiledSmall();
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
    public void ExpressionRawBig()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotRawBig();
        Assert.NotNull(result);
        Assert.Equal(_expected2, result.Evaluate());
    }

    [Fact]
    public void ExpressionCompiledBig()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotCompiledBig();
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
    public void BigJson()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.BigJson_Parlot();
        Assert.NotNull(result);
    }

    [Fact]
    public void BigJsonCompiled()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.BigJson_ParlotCompiled();
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
    public void DeepJsonCompiled()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.DeepJson_ParlotCompiled();
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
    public void LongJsonCompiled()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.LongJson_ParlotCompiled();
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
    public void WideJsonCompiled()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.WideJson_ParlotCompiled();
        Assert.NotNull(result);
    }

    [Fact]
    public void ParlotEmailCompiled()
    {
        var benchmarks = new RegexBenchmarks();
        var result = benchmarks.ParlotEmailCompiled();
        Assert.Equal(RegexBenchmarks.Email, result);
    }

    [Fact]
    public void ParlotEmail()
    {
        var benchmarks = new RegexBenchmarks();
        var result = benchmarks.ParlotEmail();
        Assert.Equal(RegexBenchmarks.Email, result);
    }

    [Fact]
    public void ParlotLookupFluent()
    {
        var benchmarks = new SwitchExpressionBenchmarks() { Length = 2 };
        benchmarks.Setup();
        var result = benchmarks.LookupMatchFluent();
    }

    [Fact]
    public void ParlotLookupCompiled()
    {
        var benchmarks = new SwitchExpressionBenchmarks() { Length = 2 };
        benchmarks.Setup();
        var result = benchmarks.LookupMatchCompiled();
    }
}
#endif
