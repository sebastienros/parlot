using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ParlotBenchmarks
    {
        private const string _stringWithEscapes = "This is a new line \\n \\t and a tab and some \\xa0";
        private const string _stringWithoutEscapes = "This is a new line \n \t and a tab and some \xa0";

        private JsonBench _jsonBench = new JsonBench();
        private ExprBench _exprBench = new ExprBench();

        [GlobalSetup]
        public void Setup()
        {
            _jsonBench.Setup();
        }

        [Benchmark, BenchmarkCategory("DecodeString")]
        public TextSpan DecodeStringWithoutEscapes()
        {
            return Character.DecodeString(_stringWithoutEscapes);
        }

        [Benchmark, BenchmarkCategory("DecodeString")]
        public TextSpan DecodeStringWithEscapes()
        {
            return Character.DecodeString(_stringWithEscapes);
        }

        [Benchmark, BenchmarkCategory("Expressions")]
        public Expression ExpressionRawSmall()
        {
            return _exprBench.ParlotRawSmall();
        }

        [Benchmark, BenchmarkCategory("Expressions")]
        public Expression ExpressionFluentSmall()
        {
            return _exprBench.ParlotFluentSmall();
        }

        [Benchmark, BenchmarkCategory("Expressions")]
        public Expression ExpressionRawBig()
        {
            return _exprBench.ParlotRawBig();
        }

        [Benchmark, BenchmarkCategory("Expressions")]
        public Expression ExpressionFluentBig()
        {
            return _exprBench.ParlotFluentBig();
        }

        [Benchmark, BenchmarkCategory("Json")]
        public IJson BigJson()
        {
            return _jsonBench.BigJson_Parlot();
        }

        [Benchmark, BenchmarkCategory("Json")]
        public IJson DeepJson()
        {
            return _jsonBench.DeepJson_Parlot();
        }

        [Benchmark, BenchmarkCategory("Json")]
        public IJson LongJson()
        {
            return _jsonBench.LongJson_Parlot();
        }

        [Benchmark, BenchmarkCategory("Json")]
        public IJson WideJson()
        {
            return _jsonBench.WideJson_Parlot();
        }
    }
}
