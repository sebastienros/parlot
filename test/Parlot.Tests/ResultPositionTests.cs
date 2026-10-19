using System.Collections.Generic;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

// The start and end of a result are the text the parser matched, and an empty match is at the position of the cursor.
public class ResultPositionTests
{
    private static ParseContext Parse<T>(Parser<T> parser, string text, out ParseResult<T> result, bool success = true)
    {
        var context = new ParseContext(new Scanner(text));
        result = new ParseResult<T>();

        Assert.Equal(success, parser.Parse(context, ref result));

        return context;
    }

    private static void AssertCursor(ParseContext context, int offset, int line, int column)
    {
        Assert.Equal(offset, context.Scanner.Cursor.Position.Offset);
        Assert.Equal(line, context.Scanner.Cursor.Position.Line);
        Assert.Equal(column, context.Scanner.Cursor.Position.Column);
    }

    [Fact]
    public void OptionalShouldBeAtTheCursorWhenEmpty()
    {
        var context = Parse(Terms.Text("ab").And(Terms.Char('c').Optional()), " ab", out var result);

        Assert.False(result.Value.Item2.HasValue);
        Assert.Equal(1, result.Start);
        Assert.Equal(3, result.End);
        AssertCursor(context, 3, 1, 4);

        context = Parse(Literals.Char('c').Optional(), "\n\nx", out var empty);

        Assert.Equal(0, empty.Start);
        Assert.Equal(0, empty.End);
        AssertCursor(context, 0, 1, 1);
    }

    [Fact]
    public void OptionalShouldIgnoreThePositionsOfARejectedMatch()
    {
        // When writes the match in the result before rejecting it
        var context = Parse(Literals.Text("ab").When(static (_, _) => false).Optional(), "ab", out var result);

        Assert.False(result.Value.HasValue);
        Assert.Equal(0, result.Start);
        Assert.Equal(0, result.End);
        AssertCursor(context, 0, 1, 1);
    }

    [Fact]
    public void OptionalShouldSpanTheMatch()
    {
        var context = Parse(Terms.Char('c').Optional(), " c", out var result);

        Assert.Equal('c', result.Value.Value);
        Assert.Equal(1, result.Start);
        Assert.Equal(2, result.End);
        AssertCursor(context, 2, 1, 3);
    }

    [Fact]
    public void ZeroOrOneShouldBeAtTheCursorWhenEmpty()
    {
        var context = Parse(Terms.Text("ab").And(ZeroOrOne(Terms.Char('c'))), " ab", out var result);

        Assert.Empty(result.Value.Item2);
        Assert.Equal(1, result.Start);
        Assert.Equal(3, result.End);
        AssertCursor(context, 3, 1, 4);

        context = Parse(ZeroOrOne(Literals.Text("ab").When(static (_, _) => false)), "ab", out var rejected);

        Assert.Empty(rejected.Value);
        Assert.Equal(0, rejected.Start);
        Assert.Equal(0, rejected.End);
        AssertCursor(context, 0, 1, 1);
    }

    [Fact]
    public void ZeroOrManyShouldBeAtTheCursorWhenEmpty()
    {
        var context = Parse(Terms.Text("ab").And(ZeroOrMany(Terms.Char('c'))), " ab", out var result);

        Assert.Empty(result.Value.Item2);
        Assert.Equal(1, result.Start);
        Assert.Equal(3, result.End);
        AssertCursor(context, 3, 1, 4);

        context = Parse(Terms.Text("ab").And(ZeroOrMany(Terms.Char('c'))), " ab\ncc", out var matched);

        Assert.Equal(2, matched.Value.Item2.Count);
        Assert.Equal(1, matched.Start);
        Assert.Equal(6, matched.End);
        AssertCursor(context, 6, 2, 3);
    }

    [Fact]
    public void TupleRightAssociativeShouldSpanTheOperand()
    {
        var parser = Terms.Integer().RightAssociative((Terms.Char('^'), static (long a, long b) => a + b));

        var context = Parse(parser, " 1", out var result);

        Assert.Equal(1, result.Value);
        Assert.Equal(1, result.Start);
        Assert.Equal(2, result.End);
        AssertCursor(context, 2, 1, 3);
    }

    [Fact]
    public void SeparatedShouldBeAtTheCursorWhenEmpty()
    {
        var context = Parse(Terms.Text("ab").And(Separated(Terms.Char(','), Terms.Integer(), min: 0)), " ab", out var result);

        Assert.Empty(result.Value.Item2);
        Assert.Equal(1, result.Start);
        Assert.Equal(3, result.End);
        AssertCursor(context, 3, 1, 4);

        var withOptions = new Separated<char, long>(Terms.Char(','), Terms.Integer(), 0, 0, removeEmptyEntries: true, allowLeadingSeparator: false, allowTrailingSeparator: false);
        context = Parse(Terms.Text("ab").And(withOptions), " ab", out var options);

        Assert.Empty(options.Value.Item2);
        Assert.Equal(1, options.Start);
        Assert.Equal(3, options.End);
        AssertCursor(context, 3, 1, 4);
    }
}
