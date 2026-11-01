using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Measures case-insensitive texts and keywords returning the matched text, on 1000 matches in mixed cases. The cases that
// differ from the text are taken from a pool. Results are added in the pull request.

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
