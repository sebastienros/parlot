using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public class AssociativeBenchmarks
{
    private const string Input = "10 - 4 + 2 - 1";

    private static readonly Parser<decimal> _leftTuples = Terms.Decimal().LeftAssociative(
        (Terms.Char('+'), static (decimal left, decimal right) => left + right),
        (Terms.Char('-'), static (decimal left, decimal right) => left - right));

    private static readonly Parser<decimal> _leftValues = Terms.Decimal().LeftAssociative(
        [Terms.Char('+'), Terms.Char('-')],
        static (decimal left, decimal right, char op) => op == '+' ? left + right : left - right);

    private static readonly Parser<decimal> _rightTuples = Terms.Decimal().RightAssociative(
        (Terms.Char('+'), static (decimal left, decimal right) => left + right),
        (Terms.Char('-'), static (decimal left, decimal right) => left - right));

    private static readonly Parser<decimal> _rightValues = Terms.Decimal().RightAssociative(
        [Terms.Char('+'), Terms.Char('-')],
        static (decimal left, decimal right, char op) => op == '+' ? left + right : left - right);

    [Benchmark(Baseline = true), BenchmarkCategory("Left")]
    public decimal Left_Tuples() => _leftTuples.Parse(Input);

    [Benchmark, BenchmarkCategory("Left")]
    public decimal Left_Values() => _leftValues.Parse(Input);

    [Benchmark(Baseline = true), BenchmarkCategory("Right")]
    public decimal Right_Tuples() => _rightTuples.Parse(Input);

    [Benchmark, BenchmarkCategory("Right")]
    public decimal Right_Values() => _rightValues.Parse(Input);
}
