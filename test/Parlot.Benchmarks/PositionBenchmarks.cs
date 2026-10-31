using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The cursor only tracks its offset: the line and the column are counted from the text when a position is read.
// Parses 2,000 identifiers on 400 lines without reading any position, reading the position after each identifier,
// which counts the lines of the text read since the previous one, and reporting an error after the last identifier,
// which counts the lines of the whole text once.
[MemoryDiagnoser]
public class PositionBenchmarks
{
    private static readonly string _text = string.Join("\n", Enumerable.Range(0, 400)
        .Select(static i => string.Join(" ", Enumerable.Range(0, 5).Select(j => "id" + i + "_" + j))));

    private static readonly Parser<int> _identifiers = ZeroOrMany(Terms.Identifier()).Then(static x => x.Count);

    private static readonly Parser<int> _positions = ZeroOrMany(Terms.Identifier().Then(static (c, _) => c.Scanner.Cursor.Position.Line))
        .Then(static x => x[x.Count - 1]);

    private static readonly Parser<int> _error = ZeroOrMany(Terms.Identifier()).AndSkip(Terms.Char(';').ElseError("Expected ';'"))
        .Then(static x => x.Count);

    public PositionBenchmarks()
    {
        if (Identifiers() != 2000) throw new InvalidOperationException(nameof(Identifiers));
        if (PositionAfterEachIdentifier() != 400) throw new InvalidOperationException(nameof(PositionAfterEachIdentifier));
        if (ErrorPositionAtTheEnd() != 400) throw new InvalidOperationException(nameof(ErrorPositionAtTheEnd));
    }

    [Benchmark(Baseline = true)]
    public int Identifiers() => _identifiers.Parse(_text);

    [Benchmark]
    public int PositionAfterEachIdentifier() => _positions.Parse(_text);

    [Benchmark]
    public int ErrorPositionAtTheEnd()
    {
        _error.TryParse(_text, out _, out var error);
        return error!.Position.Line;
    }
}
