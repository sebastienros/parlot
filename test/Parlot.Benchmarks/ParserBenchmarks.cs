using BenchmarkDotNet.Attributes;
using Parlot.Benchmarks.Pidgin;
using Parlot.Tests.Calc;
using Parlot.Fluent;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class ExpressionBenchmarks
    {
        private readonly Parser _parser = new();

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

        [Benchmark, BenchmarkCategory("Expression1")]
        public Expression FluentExpression1()
        {
            FluentParser.Expression.TryParse(Expression1, out var result);
            return result;
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

        [Benchmark, BenchmarkCategory("Expression2")]
        public Expression FluentExpression2()
        {
            FluentParser.Expression.TryParse(Expression2, out var result);
            return result;
        }
    }
}
