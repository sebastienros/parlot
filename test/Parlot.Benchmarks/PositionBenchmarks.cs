using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Linq;
using System.Text;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The cursor only tracks its offset: the line and the column are counted from the text when a position is read.
// Parses 2,000 identifiers on 400 lines without reading any position, reading the position after each identifier,
// which counts the lines of the text read since the previous one, and reporting an error after the last identifier,
// which counts the lines of the whole text once.
// The Tree benchmarks parse nested lists of identifiers, one item per line, as a parser building an AST with locations
// does: each node asks for the position of its end, then of its start, which is far behind it for the outer lists.
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

    private static readonly string _tree = BuildTree("\n");
    private static readonly string _treeCrLf = BuildTree("\r\n");

    private static readonly Parser<long> _treeWithoutLocations = BuildTreeParser(locations: false);
    private static readonly Parser<long> _treeWithLocations = BuildTreeParser(locations: true);

    // Lists of 4 items nested 6 levels deep: 4,096 identifiers on 5,461 lines
    private static string BuildTree(string newLine)
    {
        var builder = new StringBuilder();
        var count = 0;

        void Append(int depth)
        {
            if (depth == 0)
            {
                builder.Append("id").Append(count++);
                return;
            }

            builder.Append('[').Append(newLine);

            for (var i = 0; i < 4; i++)
            {
                if (i > 0)
                {
                    builder.Append(',').Append(newLine);
                }

                Append(depth - 1);
            }

            builder.Append(newLine).Append(']');
        }

        Append(6);
        return builder.ToString();
    }

    // The sum of the lines and columns of the nodes, or of their offsets
    private static Parser<long> BuildTreeParser(bool locations)
    {
        var value = Deferred<long>();

        var leaf = locations
            ? Terms.Identifier().Then(static (context, start, end, _) => Locate(context, start, end))
            : Terms.Identifier().Then(static (context, start, end, _) => (long)(start + end));

        var list = Between(Terms.Char('['), Separated(Terms.Char(','), value), Terms.Char(']'));

        var node = locations
            ? list.Then(static (context, start, end, items) => Locate(context, start, end) + items.Sum())
            : list.Then(static (context, start, end, items) => start + end + items.Sum());

        value.Parser = leaf.Or(node);
        return value.Eof();
    }

    private static long Locate(ParseContext context, int start, int end)
    {
        var cursor = context.Scanner.Cursor;
        var endPosition = cursor.GetPosition(end);
        var startPosition = cursor.GetPosition(start);
        return startPosition.Line + startPosition.Column + endPosition.Line + endPosition.Column;
    }

    public PositionBenchmarks()
    {
        if (TreeWithoutLocations() == 0) throw new InvalidOperationException(nameof(TreeWithoutLocations));
        if (TreeWithLocations() == 0) throw new InvalidOperationException(nameof(TreeWithLocations));
        if (TreeWithLocationsCrLf() != TreeWithLocations()) throw new InvalidOperationException(nameof(TreeWithLocationsCrLf));

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

    [Benchmark]
    public long TreeWithoutLocations() => _treeWithoutLocations.Parse(_tree);

    [Benchmark]
    public long TreeWithLocations() => _treeWithLocations.Parse(_tree);

    // "\r\n" ends a single line with the line terminators of JavaScript
    [Benchmark]
    public long TreeWithLocationsCrLf() => _treeWithLocations.Parse(new ParseContext(new Scanner(_treeCrLf)) { NewLines = "\n\r\u2028\u2029" });
}
