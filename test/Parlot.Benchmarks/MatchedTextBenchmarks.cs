using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Before the pool:
// | Method   | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
// |--------- |---------:|---------:|---------:|-------:|-------:|----------:|
// | Texts    | 46.55 us | 3.009 us | 2.815 us | 2.0142 | 0.1221 |  34.08 KB |
// | Keywords | 48.53 us | 5.080 us | 4.752 us | 2.0142 | 0.1221 |  34.08 KB |
//
// With the pool:
// | Method   | Mean     | Error    | StdDev   | Gen0   | Allocated |
// |--------- |---------:|---------:|---------:|-------:|----------:|
// | Texts    | 44.81 us | 4.030 us | 3.770 us | 0.4272 |   8.02 KB |
// | Keywords | 44.32 us | 3.469 us | 2.896 us | 0.4272 |   8.02 KB |

// Measures case-insensitive texts and keywords returning the matched text, on 1000 matches in mixed cases. The cases that
// differ from the text are taken from a pool. The 8 KB left are the result list.

[MemoryDiagnoser, ShortRunJob]
public class MatchedTextBenchmarks
{
    private readonly Parser<IReadOnlyList<string>> _texts = ZeroOrMany(Terms.Text("select", caseInsensitive: true, returnMatchedText: true)).Eof();
    private readonly Parser<IReadOnlyList<string>> _keywords = ZeroOrMany(Terms.Keyword("select", caseInsensitive: true, returnMatchedText: true)).Eof();

    private string _input = "";

    [GlobalSetup]
    public void Setup()
    {
        var cases = new[] { "SELECT", "select", "Select" };
        _input = string.Join(" ", Enumerable.Range(0, 1000).Select(i => cases[i % cases.Length]));

        if (Texts().Count != 1000 || Keywords().Count != 1000)
        {
            throw new InvalidOperationException("Incorrect parsing.");
        }
    }

    [Benchmark]
    public IReadOnlyList<string> Texts() => _texts.Parse(_input)!;

    [Benchmark]
    public IReadOnlyList<string> Keywords() => _keywords.Parse(_input)!;
}
