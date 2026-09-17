using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Parlot.Benchmarks;

// Low cardinality reaches steady-state hits; 8192 cycles beyond the 4096-slot cache.
// The cursor persists across invocations, so the churn case does not repeat one batch.
[MemoryDiagnoser, ShortRunJob]
public class ApexStringCacheBenchmarks
{
    private readonly Consumer _consumer = new();
    private string[] _values;
    private int _offset;

    [Params(1, 8192)] public int Distinct { get; set; }
    [Params(16, 256, 1024)] public int Length { get; set; }
    [Params(false, true)] public bool Escaped { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _values = new string[Distinct];
        for (var i = 0; i < Distinct; i++)
        {
            var value = i.ToString("D8") + new string('x', Length - 8);
            _values[i] = Escaped ? value + @"\n" : "[" + value + "]";
        }
    }

    [Benchmark(OperationsPerInvoke = 256)]
    public void Materialize()
    {
        for (var i = 0; i < 256; i++)
        {
            var value = _values[_offset];
            _offset = (_offset + 1) & (Distinct - 1);
            _consumer.Consume(Escaped
                ? Character.DecodeString(value)
                : new TextSpan(value, 1, value.Length - 2).ToString());
        }
    }
}
