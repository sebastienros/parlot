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

    // The conclusion above holds for the identifier sets, which is why Character.IsIdentifierStart and
    // Character.IsIdentifierPart use SearchValues.Contains on net8.0+. It does not carry over to the
    // digit sets: char.IsAsciiDigit is a single range check and char.IsAsciiHexDigit is a branchless
    // 64-bit shift-and-mask, so neither touches memory at all. DecimalDigits and HexDigits are exactly
    // the ASCII digit and ASCII hex-digit sets, so the BCL predicates are drop-in equivalents.

    // BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200)
    // Job = MediumRun  IterationCount=15  LaunchCount=2  WarmupCount=10
    //
    // | Method                                    | Mean      | Error     | StdDev    |
    // |------------------------------------------ |----------:|----------:|----------:|
    // | SearchValuesContains_IsDecimalDigit_True  | 0.1911 ns | 0.0066 ns | 0.0092 ns |
    // | SearchValuesContains_IsDecimalDigit_False | 0.1945 ns | 0.0094 ns | 0.0132 ns |
    // | IsAsciiDigit_True                         | 0.0159 ns | 0.0165 ns | 0.0246 ns |
    // | IsAsciiDigit_False                        | 0.0138 ns | 0.0231 ns | 0.0323 ns |
    // | SearchValuesContains_IsHexDigit_True      | 0.1163 ns | 0.0215 ns | 0.0309 ns |
    // | SearchValuesContains_IsHexDigit_False     | 0.2146 ns | 0.0670 ns | 0.0983 ns |
    // | IsAsciiHexDigit_True                      | 0.0000 ns | 0.0000 ns | 0.0000 ns |
    // | IsAsciiHexDigit_False                     | 0.0074 ns | 0.0061 ns | 0.0089 ns |
    //
    // CONCLUSION: char.IsAscii* wins by an order of magnitude for the digit sets, so Character.IsDecimalDigit
    // and Character.IsHexDigit use them on every target framework. The IsAscii* rows are at the resolution
    // floor -- they inline to a couple of instructions -- so treat them as "free" rather than as exact values.

    private static readonly SearchValues<char> _decimalDigits = SearchValues.Create(Character.DecimalDigits);
    private static readonly SearchValues<char> _hexDigits = SearchValues.Create(Character.HexDigits);

    private static readonly char _digit = '7';
    private static readonly char _notDigit = 'z';
    private static readonly char _hexDigit = 'e';
    private static readonly char _notHexDigit = 'z';

    [Benchmark]
    public bool SearchValuesContains_IsDecimalDigit_True() => _decimalDigits.Contains(_digit);

    [Benchmark]
    public bool SearchValuesContains_IsDecimalDigit_False() => _decimalDigits.Contains(_notDigit);

    [Benchmark]
    public bool IsAsciiDigit_True() => char.IsAsciiDigit(_digit);

    [Benchmark]
    public bool IsAsciiDigit_False() => char.IsAsciiDigit(_notDigit);

    [Benchmark]
    public bool SearchValuesContains_IsHexDigit_True() => _hexDigits.Contains(_hexDigit);

    [Benchmark]
    public bool SearchValuesContains_IsHexDigit_False() => _hexDigits.Contains(_notHexDigit);

    [Benchmark]
    public bool IsAsciiHexDigit_True() => char.IsAsciiHexDigit(_hexDigit);

    [Benchmark]
    public bool IsAsciiHexDigit_False() => char.IsAsciiHexDigit(_notHexDigit);
}
#endif