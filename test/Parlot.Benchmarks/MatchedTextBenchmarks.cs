using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Before the cases were kept, each match of another case allocated a string:
// | Method   | Mean     | Allocated |
// |--------- |---------:|----------:|
// | Texts    | 46.55 us |  34.08 KB |
// | Keywords | 48.53 us |  34.08 KB |
//
// With a StringPool per parser:
// | Method        | Mean      | Allocated |
// |-------------- |----------:|----------:|
// | Texts         | 42.90 us  |   8.02 KB |
// | Keywords      | 46.26 us  |   8.02 KB |
// | BuildAndMatch | 598.5 ns  |   3.58 KB |
//
// With the four cases of CaseCache:
// | Method        | Mean      | Allocated |
// |-------------- |----------:|----------:|
// | Texts         | 40.76 us  |   8.02 KB |
// | Keywords      | 43.09 us  |   8.02 KB |
// | BuildAndMatch | 186.6 ns  |     512 B |

// Measures case-insensitive texts and keywords returning the matched text, on 1000 matches in mixed cases. The cases that
// differ from the text are kept by the parser. The 8 KB left are the result list.

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

    // What a parser costs: it is built and matches a case that differs from its text once
    [Benchmark]
    public string BuildAndMatch() => Terms.Keyword("select", caseInsensitive: true, returnMatchedText: true).Parse(" SELECT")!;
}
