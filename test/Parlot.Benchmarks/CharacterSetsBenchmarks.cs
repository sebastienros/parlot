#if NET8_0_OR_GREATER
using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
namespace Parlot.Benchmarks;

// BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.2314)
// 12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
// .NET SDK 9.0.100
//   [Host]   : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX2
//   ShortRun : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX2

// Job = ShortRun  IterationCount=3  LaunchCount=1
// WarmupCount=3

// | Method                                        | Mean      | Error     | StdDev    | Allocated |
// |---------------------------------------------- |----------:|----------:|----------:|----------:|
// | Character_IsIdentifierPart_True               | 0.7820 ns | 0.1447 ns | 0.0079 ns |         - |
// | Character_IsIdentifierPart_False              | 0.8523 ns | 0.8127 ns | 0.0445 ns |         - |
// | SearchValuesIndexOfAny_IsIdentifierPart_True  | 2.9817 ns | 1.8415 ns | 0.1009 ns |         - |
// | SearchValuesIndexOfAny_IsIdentifierPart_False | 3.4020 ns | 1.0075 ns | 0.0552 ns |         - |
// | SearchValuesContains_IsIdentifierPart_True    | 0.3611 ns | 0.5977 ns | 0.0328 ns |         - |
// | SearchValuesContains_IsIdentifierPart_False   | 0.3762 ns | 0.6787 ns | 0.0372 ns |         - |

// SearchValue.Contains has the best performance for this scenario. This could be explain by the fact that each SearchValue instance is optimized for searching a specific set of values,
// even in the Contains case, using lookups when more adapted that pure range checks.

[MemoryDiagnoser, ShortRunJob]
public class CharacterSetsBenchmarks
{
    private static SearchValues<char> _identifierPart = SearchValues.Create("$_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
    private static string _identifier1 = "stream";
    private static string _identifier2 = "+123-567";

    public CharacterSetsBenchmarks()
    {
        if (!Character_IsIdentifierPart_True()) throw new InvalidOperationException(nameof(Character_IsIdentifierPart_True));
        if (Character_IsIdentifierPart_False()) throw new InvalidOperationException(nameof(Character_IsIdentifierPart_False));
        if (!SearchValuesIndexOfAny_IsIdentifierPart_True()) throw new InvalidOperationException(nameof(SearchValuesIndexOfAny_IsIdentifierPart_True));
        if (SearchValuesIndexOfAny_IsIdentifierPart_False()) throw new InvalidOperationException(nameof(SearchValuesIndexOfAny_IsIdentifierPart_False));
        if (!SearchValuesContains_IsIdentifierPart_True()) throw new InvalidOperationException(nameof(SearchValuesContains_IsIdentifierPart_True));
        if (SearchValuesContains_IsIdentifierPart_False()) throw new InvalidOperationException(nameof(SearchValuesContains_IsIdentifierPart_False));
    }

    [Benchmark]
    public bool Character_IsIdentifierPart_True()
    {
        return Character.IsIdentifierPart(_identifier1[0]);
    }

    [Benchmark]
    public bool Character_IsIdentifierPart_False()
    {
        return Character.IsIdentifierPart(_identifier2[0]);
    }

    [Benchmark]
    public bool SearchValuesIndexOfAny_IsIdentifierPart_True()
    {
        return _identifier1.AsSpan().IndexOfAny(_identifierPart) == 0;
    }

    [Benchmark]
    public bool SearchValuesIndexOfAny_IsIdentifierPart_False()
    {
        return _identifier2.AsSpan().IndexOfAny(_identifierPart) == 0;
    }

    [Benchmark]
    public bool SearchValuesContains_IsIdentifierPart_True()
    {
        return _identifierPart.Contains(_identifier1[0]);
    }

    [Benchmark]
    public bool SearchValuesContains_IsIdentifierPart_False()
    {
        return _identifierPart.Contains(_identifier2[0]);
    }
}
#endif