using Parlot.Benchmarks;
using Xunit;

namespace Parlot.Tests
{
    public class BenchmarksTests
    {
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
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.ExpressionRawSmall();
            Assert.NotNull(result);
        }

        [Fact]
        public void ExpressionCompiledSmall()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.ExpressionCompiledSmall();
            Assert.NotNull(result);
        }

        [Fact]
        public void ExpressionFluentSmall()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.ExpressionCompiledSmall();
            Assert.NotNull(result);
        }

        [Fact]
        public void ExpressionRawBig()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.ExpressionRawBig();
            Assert.NotNull(result);
        }

        [Fact]
        public void ExpressionCompiledBig()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.ExpressionCompiledBig();
            Assert.NotNull(result);
        }

        [Fact]
        public void ExpressionFluentBig()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.ExpressionCompiledBig();
            Assert.NotNull(result);
        }

        [Fact]
        public void BigJson()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.BigJson();
            Assert.NotNull(result);
        }

        [Fact]
        public void BigJsonCompiled()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.BigJsonCompiled();
            Assert.NotNull(result);
        }

        [Fact]
        public void DeepJson()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.DeepJson();
            Assert.NotNull(result);
        }

        [Fact]
        public void DeepJsonCompiled()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.DeepJsonCompiled();
            Assert.NotNull(result);
        }

        [Fact]
        public void LongJson()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.LongJson();
            Assert.NotNull(result);
        }

        [Fact]
        public void LongJsonCompiled()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.LongJsonCompiled();
            Assert.NotNull(result);
        }

        [Fact]
        public void WideJson()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.WideJson();
        }

        [Fact]
        public void WideJsonCompiled()
        {
            var benchmarks = new ParlotBenchmarks();
            benchmarks.Setup();
            var result = benchmarks.WideJsonCompiled();
            Assert.NotNull(result);
        }

        [Fact]
        public void ParlotEmailCompiled()
        {
            var benchmarks = new RegexBenchmarks();
            var result = benchmarks.ParlotEmailCompiled();
            Assert.True(result);
        }

        [Fact]
        public void ParlotEmail()
        {
            var benchmarks = new RegexBenchmarks();
            var result = benchmarks.ParlotEmail();
            Assert.True(result);
        }
    }
}
