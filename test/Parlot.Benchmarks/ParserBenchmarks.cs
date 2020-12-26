using BenchmarkDotNet.Attributes;
using Parlot.Benchmarks.Pidgin;
using Parlot.Tests.Calc;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class ExpressionBenchmarks
    {
        private readonly Parser _parser = new Parser();

        private const string Expression1 = "1 + 2";
        private const string Expression2 = "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

        [Benchmark, BenchmarkCategory("Expression1")]
        public Expression PidginExpression1()
        {
            return ExprParser.ParseOrThrow(Expression1);
        }

        [Benchmark, BenchmarkCategory("Expression1")]
        public Expression ParlotExpression1()
        {
            return _parser.Parse(Expression1);
        }

        [Benchmark, BenchmarkCategory("Expression2")]
        public Expression PidginExpression2()
        {
            return ExprParser.ParseOrThrow(Expression2);
        }

        [Benchmark, BenchmarkCategory("Expression2")]
        public Expression ParlotExpression2()
        {
            return _parser.Parse(Expression2);
        }
    }
}