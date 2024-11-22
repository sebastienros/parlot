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
   
// | Method                          | Mean      | Error     | StdDev    | Allocated |
// |-------------------------------- |----------:|----------:|----------:|----------:|
// | NaiveIdentifierMatch            | 11.216 ns | 1.0236 ns | 0.0561 ns |         - |
// | SearchValuesIdentifierMatch     |  2.470 ns | 0.3889 ns | 0.0213 ns |         - |
// | NaiveIdentifierAndContainsMatch | 14.230 ns | 1.3360 ns | 0.0732 ns |         - |

// On top of a better raw performance, SearchValues usage allows to call Cursor.Advance(n) only once.

[MemoryDiagnoser, ShortRunJob]
public class IdentifiersBenchmarks
{
    private static SearchValues<char> _identifierStart = SearchValues.Create("$_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
    private static SearchValues<char> _identifierPart = SearchValues.Create("$_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
    private static string _identifier1 = "workflowIdentifier = 123";
    private static int _length = 18;
    
    public IdentifiersBenchmarks()
    {
        if (NaiveIdentifierMatch() != _length) throw new InvalidOperationException(nameof(NaiveIdentifierMatch));
        if (SearchValuesIdentifierMatch() != _length) throw new InvalidOperationException(nameof(SearchValuesIdentifierMatch));
        if (NaiveIdentifierAndContainsMatch() != _length) throw new InvalidOperationException(nameof(NaiveIdentifierAndContainsMatch));
    }

    [Benchmark]
    public int NaiveIdentifierMatch()
    {
        bool isIdentifier = Character.IsIdentifierStart(_identifier1[0]);

        if (!isIdentifier)
        {
            return -1;
        }

        for (var i = 1; i < _identifier1.Length; i++)
        {
            if (!Character.IsIdentifierPart(_identifier1[i]))
            {
                return i;
            }
        }

        return -1;
    }

    [Benchmark]
    public int SearchValuesIdentifierMatch()
    {
        bool isIdentifier = _identifierStart.Contains(_identifier1[0]);

        if (!isIdentifier)
        {
            return -1;
        }

        return _identifier1.AsSpan().Slice(1).IndexOfAnyExcept(_identifierPart) + 1;
    }

    [Benchmark]
    public int NaiveIdentifierAndContainsMatch()
    {
        bool isIdentifier = _identifierStart.Contains(_identifier1[0]);

        if (!isIdentifier)
        {
            return -1;
        }

        for (var i = 1; i < _identifier1.Length; i++)
        {
            if (!_identifierPart.Contains(_identifier1[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
#endif