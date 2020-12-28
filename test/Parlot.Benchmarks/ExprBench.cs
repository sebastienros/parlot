using BenchmarkDotNet.Attributes;
using Parlot.Benchmarks.PidginParsers;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using System;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
#pragma warning disable CA1822 // Mark members as static
    public class ExprBench
    {
        private readonly Parser _parser = new();

        private const string Expression1 = "3 - 1 / 2 + 1";
        private const string Expression2 = "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

        public ExprBench()
        {
            var expected1 = (decimal)3.5;
            var expected2 = (decimal)-64.5;

            if (PidginExpression1().Evaluate() != expected1) throw new Exception("PidginExpression1");
            if (ParlotExpression1().Evaluate() != expected1) throw new Exception("ParlotExpression1");
            if (FluentExpression1().Evaluate() != expected1) throw new Exception("FluentExpression1");

            if (PidginExpression2().Evaluate() != expected2) throw new Exception("PidginExpression2");
            if (ParlotExpression2().Evaluate() != expected2) throw new Exception("ParlotExpression2");
            if (FluentExpression2().Evaluate() != expected2) throw new Exception("FluentExpression2");
        }

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
#pragma warning restore CA1822 // Mark members as static
}
