using System;
using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// The runtime Between, which reports a result spanning its delimiters.
/// </summary>
[MemoryDiagnoser, ShortRunJob]
public class BetweenBenchmarks
{
    private const string KeyInput = " [ key ] ";
    private const string ListInput = " [ 1, 2, 3, 4, 5, 6, 7, 8 ] ";

    private static readonly Parser<TextSpan> Key = Between(Terms.Char('['), Terms.Identifier(), Terms.Char(']'));
    private static readonly Parser<int> KeySpan = Key.Then(static (_, start, end, _) => end - start);

    // Capture invokes Between without its value, through ParseWithoutValue
    private static readonly Parser<TextSpan> CapturedList = Capture(Between(Terms.Char('['), Separated(Terms.Char(','), Terms.Integer()), Terms.Char(']')));

    [GlobalSetup]
    public void Setup()
    {
        if (BetweenValue() != "key" || BetweenSpan() <= 0 || BetweenWithoutValue() != ListInput.TrimEnd())
        {
            throw new InvalidOperationException("Incorrect Between parsing.");
        }
    }

    [Benchmark]
    public string BetweenValue() => Key.Parse(KeyInput).ToString();

    [Benchmark]
    public int BetweenSpan() => KeySpan.Parse(KeyInput);

    [Benchmark]
    public string BetweenWithoutValue() => CapturedList.Parse(ListInput).ToString();
}
