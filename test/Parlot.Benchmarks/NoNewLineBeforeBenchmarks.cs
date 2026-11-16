using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// NoNewLineBefore alternatives of choices which skip the white space for their alternatives, as JavaScript's postfix
// operators and arrows. Before #551 such a choice had no lookup table and tried its alternatives in order; it now selects
// them with the char after the white space. The Plain rows are the same grammars without NoNewLineBefore.

[MemoryDiagnoser, ShortRunJob, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), CategoriesColumn]
public class NoNewLineBeforeBenchmarks
{
    // 200 expressions of 6 operands, with postfix operators and binary operators which start with the same chars
    private static readonly string Expressions = string.Join("\n", Enumerable.Range(0, 200).Select(static i => $"a{i}++ * b - c-- + d / e++ - f"));

    // 800 statements: assignments, arrows, and arrows after a new line, which start a statement of their own
    private static readonly string Statements = string.Join("\n", Enumerable.Range(0, 200).Select(static i => $"a{i} = b c => d e\n=> f"));

    // 1000 statements, of which one in five has an assignment tail and one in ten an arrow tail, as conditional expressions
    private static readonly string Tails = string.Concat(Enumerable.Range(0, 1000).Select(static i => (i % 10) switch
    {
        0 or 5 => $"a{i} >>= b;",
        3 => $"c{i} => d;\n",
        _ => $"e{i};\n",
    })).TrimEnd();

    private static readonly string[] AssignmentOperators = ["=", "+=", "-=", "*=", "/=", "%=", "<<=", ">>=", ">>>=", "&=", "|=", "^=", "**=", "&&=", "||=", "??="];

    private readonly Parser<int> _twoAlternativeTails = CreateTwoAlternativeTails();
    private readonly Parser<int> _perOperatorTails = CreatePerOperatorTails();

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

    // The tail of a JavaScript conditional expression: an assignment operator and its operand, or an arrow which can't follow a
    // new line. Before #581 the choice of these two alternatives tried them in order.
    private static Parser<int> CreateTwoAlternativeTails()
    {
        var assignmentOperator = OneOf(AssignmentOperators.Select(static o => Terms.Text(o)).ToArray());
        var assignment = assignmentOperator.And(Terms.Identifier()).Then(static _ => 1);
        var arrow = NoNewLineBefore(Terms.Text("=>")).SkipAnd(Terms.Identifier()).Then(static _ => 2);
        return TailStatements(OneOf(assignment, arrow));
    }

    // The same tail, written with an alternative per operator
    private static Parser<int> CreatePerOperatorTails()
    {
        var arrow = NoNewLineBefore(Terms.Text("=>")).SkipAnd(Terms.Identifier()).Then(static _ => 2);
        return TailStatements(OneOf(AssignmentOperators.Select(static o => Terms.Text(o).SkipAnd(Terms.Identifier()).Then(static _ => 1)).Append(arrow).ToArray()));
    }

    private static Parser<int> TailStatements(Parser<int> tail)
        => ZeroOrMany(Terms.Identifier().SkipAnd(tail.Else(0)).AndSkip(Terms.Char(';'))).Eof().Then(static x => x.Sum());

    [GlobalSetup]
    public void Setup()
    {
        if (PostfixTable() != 200 || PlainPostfixTable() != 200 || Arrows() != 800 || PlainArrows() != 600
            || PostfixTableGenerated() != 200 || ArrowsGenerated() != 800 || TwoAlternativeTails() != 400 || PerOperatorTails() != 400)
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

    [Benchmark(Baseline = true), BenchmarkCategory("Tails")]
    public int TwoAlternativeTails() => _twoAlternativeTails.Parse(Tails);

    [Benchmark, BenchmarkCategory("Tails")]
    public int PerOperatorTails() => _perOperatorTails.Parse(Tails);

    [Benchmark, BenchmarkCategory("Arrows")]
    public int ArrowsGenerated() => NoNewLineBeforeGenerated.TryParseArrows(Statements, out var value) ? value : -1;
}

public static partial class NoNewLineBeforeGenerated
{
    public static partial bool TryParsePostfixTable(string text, out int value);

    public static partial bool TryParseArrows(string text, out int value);
}
