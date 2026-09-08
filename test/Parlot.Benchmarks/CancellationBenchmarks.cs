using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public partial class CancellationBenchmarks
{
    private static readonly Parser<int> _parser = OneOrMany(Literals.Char('x')).Then(static values => values.Count).Eof();
    private readonly string _input = new('x', 128);
    private CancellationTokenSource _source;
    private CancellationToken _token;

    [Params(false, true)]
    public bool CanBeCanceled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _source = new CancellationTokenSource();
        _token = CanBeCanceled ? _source.Token : CancellationToken.None;
        if (Fluent() != _input.Length || Generated() != _input.Length || GeneratedWithoutToken() != _input.Length)
        {
            throw new InvalidOperationException("Cancellation benchmark returned an unexpected result.");
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _source.Dispose();

    [Benchmark(Baseline = true)]
    public int Fluent()
    {
        var context = new ParseContext(new Scanner(_input), _token);
        _parser.TryParse(context, out var value, out _);
        return value;
    }

    [Benchmark]
    public int Generated()
    {
        TryParse(_input, _token, out var value);
        return value;
    }

    [Benchmark]
    public int GeneratedWithoutToken()
    {
        TryParseWithoutToken(_input, out var value);
        return value;
    }

    private static partial bool TryParse(string text, CancellationToken cancellationToken, out int value);
    private static partial bool TryParseWithoutToken(string text, out int value);
}
