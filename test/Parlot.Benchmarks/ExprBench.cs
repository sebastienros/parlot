using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Benchmarks.PidginParsers;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using System;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
#pragma warning disable CA1822 // Mark members as static
    public class ExprBench
    {
        private readonly Parser _parser = new();
        private readonly Func<ParseContext, Expression> _compiled = Tests.CompileTests.Compile(FluentParser.Expression);

        private const string Expression1 = "3 - 1 / 2 + 1";
        private const string Expression2 = "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

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
            return _parser.Parse(Expression1);
        }

        //[Benchmark, BenchmarkCategory("Expression1")]
        public Expression ParlotFluentSmall()
        {
            FluentParser.Expression.TryParse(Expression1, out var result);
            return result;
        }

        [Benchmark, BenchmarkCategory("Expression1")]
        public Expression ParlotCompiledSmall()
        {
            var scanner = new Scanner(Expression1);
            var context = new ParseContext(scanner);

            return _compiled(context);
        }

        //[Benchmark, BenchmarkCategory("Expression1")]
        public Expression PidginSmall()
        {
            return ExprParser.ParseOrThrow(Expression1);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Expression2")]
        public Expression ParlotRawBig()
        {
            return _parser.Parse(Expression2);
        }

        //[Benchmark, BenchmarkCategory("Expression2")]
        public Expression ParlotFluentBig()
        {
            FluentParser.Expression.TryParse(Expression2, out var result);
            return result;
        }

        [Benchmark, BenchmarkCategory("Expression2")]
        public Expression ParlotCompiledBig()
        {
            var scanner = new Scanner(Expression2);
            var context = new ParseContext(scanner);

            return _compiled(context);
        }

        //[Benchmark, BenchmarkCategory("Expression2")]
        public Expression PidginBig()
        {
            return ExprParser.ParseOrThrow(Expression2);
        }
    }
#pragma warning restore CA1822 // Mark members as static
}
