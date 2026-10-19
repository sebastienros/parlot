using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Linq;
using System.Reflection;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Measures the choice built with the | operator against the one built with Or(), with value-type results.
// The operator used to wrap its operands in adapters which boxed every result.

[MemoryDiagnoser, ShortRunJob]
public class OperatorBenchmarks
{
    private readonly Parser<long> _or = Terms.Integer().Or(Terms.Decimal().Then(static d => (long)d));
    private readonly Parser<long> _operator = BitwiseOr(Terms.Integer(), Terms.Decimal().Then(static d => (long)d));

    private readonly ParseContext _context = new(new Scanner(" 123"));

    [GlobalSetup]
    public void Setup()
    {
        if (Or() != 123 || Operator() != 123)
        {
            throw new InvalidOperationException("Incorrect choice parsing.");
        }
    }

    [Benchmark(Baseline = true)]
    public long Or() => Parse(_or);

    [Benchmark]
    public long Operator() => Parse(_operator);

    // The | operator is an extension operator, which this project can't use as it is compiled as C# 13
    private static Parser<long> BitwiseOr(Parser<long> left, Parser<long> right)
    {
        var method = typeof(ParserOperatorExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(static m => m.Name == "op_BitwiseOr" && m.GetParameters().All(static p => p.ParameterType.GetGenericTypeDefinition() == typeof(IParser<>)))
            .MakeGenericMethod(typeof(long));

        return (Parser<long>)method.Invoke(null, [left, right]);
    }

    private long Parse(Parser<long> parser)
    {
        var result = new ParseResult<long>();

        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        parser.Parse(_context, ref result);

        return result.Value;
    }
}
