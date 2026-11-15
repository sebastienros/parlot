using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// NoNewLineBefore alternatives of choices which skip the white space for their alternatives, as JavaScript's postfix
// operators and arrows. Before #551 such a choice had no lookup table and tried its alternatives in order; it now selects
// them with the char after the white space. The Plain rows are the same grammars without NoNewLineBefore.

[MemoryDiagnoser, ShortRunJob]
public class NoNewLineBeforeBenchmarks
{
    // 200 expressions of 6 operands, with postfix operators and binary operators which start with the same chars
    private static readonly string Expressions = string.Join("\n", Enumerable.Range(0, 200).Select(static i => $"a{i}++ * b - c-- + d / e++ - f"));

    // 800 statements: assignments, arrows, and arrows after a new line, which start a statement of their own
    private static readonly string Statements = string.Join("\n", Enumerable.Range(0, 200).Select(static i => $"a{i} = b c => d e\n=> f"));

    private readonly Parser<int> _postfixTable = PostfixTable(NoNewLineBefore);
    private readonly Parser<int> _plainPostfixTable = PostfixTable(static p => p);
    private readonly Parser<int> _arrows = Arrows(NoNewLineBefore);
    private readonly Parser<int> _plainArrows = Arrows(static p => p);

    private static Parser<int> PostfixTable(Func<Parser<string>, Parser<string>> restrict)
    {
        var table = Terms.Identifier().Then(static _ => 1).Operators()
            .Postfix((restrict(Terms.Text("++")), static x => x + 1), (restrict(Terms.Text("--")), static x => x + 1))
            .LeftAssociative((Terms.Char('*'), static (a, b) => a + b), (Terms.Char('/'), static (a, b) => a + b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => a + b), (Terms.Char('-'), static (a, b) => a + b));

        return ZeroOrMany(table).Eof().Then(static x => x.Count);
    }

    private static Parser<int> Arrows(Func<Parser<string>, Parser<string>> restrict)
    {
        var assignment = OneOf(Terms.Text("="), Terms.Text("+=")).And(Terms.Identifier()).Then(static _ => 1);
        var arrow = restrict(Terms.Text("=>")).And(Terms.Identifier()).Then(static _ => 2);
        var statement = Terms.Identifier().And(OneOf(assignment, arrow).Else(0)).Then(static x => x.Item2);
        var arrowStatement = Terms.Text("=>").And(Terms.Identifier()).Then(static _ => 3);

        return ZeroOrMany(OneOf(statement, arrowStatement)).Eof().Then(static x => x.Count);
    }

    [GlobalSetup]
    public void Setup()
    {
        if (PostfixTable() != 200 || PlainPostfixTable() != 200 || Arrows() != 800 || PlainArrows() != 600
            || PostfixTableGenerated() != 200 || ArrowsGenerated() != 800)
        {
            throw new InvalidOperationException("Incorrect NoNewLineBefore parsing.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Postfix")]
    public int PostfixTable() => _postfixTable.Parse(Expressions);

    [Benchmark, BenchmarkCategory("Postfix")]
    public int PlainPostfixTable() => _plainPostfixTable.Parse(Expressions);

    [Benchmark, BenchmarkCategory("Postfix")]
    public int PostfixTableGenerated() => NoNewLineBeforeGenerated.TryParsePostfixTable(Expressions, out var value) ? value : -1;

    [Benchmark(Baseline = true), BenchmarkCategory("Arrows")]
    public int Arrows() => _arrows.Parse(Statements);

    // The arrows after a new line are read by the statements before them: fewer statements
    [Benchmark, BenchmarkCategory("Arrows")]
    public int PlainArrows() => _plainArrows.Parse(Statements);

    [Benchmark, BenchmarkCategory("Arrows")]
    public int ArrowsGenerated() => NoNewLineBeforeGenerated.TryParseArrows(Statements, out var value) ? value : -1;
}

public static partial class NoNewLineBeforeGenerated
{
    public static partial bool TryParsePostfixTable(string text, out int value);

    public static partial bool TryParseArrows(string text, out int value);
}
