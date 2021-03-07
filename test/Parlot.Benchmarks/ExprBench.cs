using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Benchmarks.PidginParsers;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using System;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class ExprBench
    {
        private readonly Parser _parser = new();
        private readonly Parser<Expression> _compiled = FluentParser.Expression.Compile();

        private const string _expression1 = "3 - 1 / 2 + 1";
        private const string _expression2 = "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

        public ExprBench()
        {
            var expected1 = (decimal)3.5;
            var expected2 = (decimal)-64.5;

            if (PidginSmall().Evaluate() != expected1) throw new Exception(nameof(PidginSmall));
            if (ParlotRawSmall().Evaluate() != expected1) throw new Exception(nameof(ParlotRawSmall));
            if (ParlotFluentSmall().Evaluate() != expected1) throw new Exception(nameof(ParlotFluentSmall));
            if (ParlotCompiledSmall().Evaluate() != expected1) throw new Exception(nameof(ParlotCompiledSmall));

            if (PidginBig().Evaluate() != expected2) throw new Exception(nameof(PidginBig));
            if (ParlotRawBig().Evaluate() != expected2) throw new Exception(nameof(ParlotRawBig));
            if (ParlotFluentBig().Evaluate() != expected2) throw new Exception(nameof(ParlotFluentBig));
            if (ParlotCompiledBig().Evaluate() != expected2) throw new Exception(nameof(ParlotCompiledBig));
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Expression1")]
        public Expression ParlotRawSmall()
        {
            return _parser.Parse(_expression1);
        }

        [Benchmark, BenchmarkCategory("Expression1")]
        public Expression ParlotCompiledSmall()
        {
            return _compiled.Parse(_expression1);
        }

        [Benchmark, BenchmarkCategory("Expression1")]
        public Expression ParlotFluentSmall()
        {
            _ = FluentParser.Expression.TryParse(_expression1, out var result);
            return result;
        }

        [Benchmark, BenchmarkCategory("Expression1")]
        public Expression PidginSmall()
        {
            return ExprParser.ParseOrThrow(_expression1);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Expression2")]
        public Expression ParlotRawBig()
        {
            return _parser.Parse(_expression2);
        }

        [Benchmark, BenchmarkCategory("Expression2")]
        public Expression ParlotCompiledBig()
        {
            return _compiled.Parse(_expression2);
        }

        [Benchmark, BenchmarkCategory("Expression2")]
        public Expression ParlotFluentBig()
        {
            _ = FluentParser.Expression.TryParse(_expression2, out var result);
            return result;
        }

        [Benchmark, BenchmarkCategory("Expression2")]
        public Expression PidginBig()
        {
            return ExprParser.ParseOrThrow(_expression2);
        }
    }
}
