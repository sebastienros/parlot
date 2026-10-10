using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class LeftAssociativeEmptyTests
{
    [Theory]
    [InlineData(false, false, "x+x+y", 3, 4)]
    [InlineData(true, false, "x+x+y", 3, 4)]
    [InlineData(false, true, "x+x+y", 3, 4)]
    [InlineData(true, true, "x+x+y", 3, 4)]
    [InlineData(false, false, "y", 1, 0)]
    [InlineData(true, false, "y", 1, 0)]
    [InlineData(false, true, "y", 1, 0)]
    [InlineData(true, true, "y", 1, 0)]
    [InlineData(false, false, "x+", 2, 2)]
    [InlineData(true, false, "x+", 2, 2)]
    [InlineData(false, true, "x+", 2, 2)]
    [InlineData(true, true, "x+", 2, 2)]
    public void StopsWithoutProgress(bool compiled, bool withContext, string text, int expected, int offset)
    {
        var operand = Literals.Text("x").Optional().Then(static _ => 1);
        var op = Literals.Text("+").Optional();
        var calls = 0;
        var parser = withContext
            ? operand.LeftAssociative((op, (ParseContext _, int a, int b) => { calls++; return a + b; }))
            : operand.LeftAssociative((op, (int a, int b) => { calls++; return a + b; }));
        if (compiled) parser = parser.Compile();

        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<int>();
        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(expected, result.Value);
        Assert.Equal(offset, context.Scanner.Cursor.Offset);
        Assert.Equal(expected - 1, calls);
    }
}
