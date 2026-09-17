using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using Parlot.Tests.Json;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class JsonLastStringParserBenchmarks
{
    private string _input;
    [Params(1, 4, 256)] public int Distinct { get; set; }
    [Params(false, true)] public bool Objects { get; set; }
    [Params(false, true)] public bool Escaped { get; set; }
    [Params(false, true)] public bool Optimize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var builder = new StringBuilder("[");
        for (var i = 0; i < 256; i++)
        {
            if (i != 0) builder.Append(',');
            if (Objects) builder.Append("{\"name\":");
            builder.Append("\"identifier").Append(i % Distinct);
            if (Escaped) builder.Append("\\n");
            builder.Append('"');
            if (Objects) builder.Append('}');
        }
        _input = builder.Append(']').ToString();
        if (Parse() is not JsonArray { Elements.Count: 256 }) throw new InvalidOperationException("JSON allocation corpus failed.");
    }

    [Benchmark]
    public IJson Parse() => JsonLastStringParser.Parse(_input, Optimize);
}
