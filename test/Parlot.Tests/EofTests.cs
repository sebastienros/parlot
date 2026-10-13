#nullable enable

using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class EofTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("42", 2)]
    [InlineData("a\n", 2)]
    public void StandaloneEofSucceedsWithoutConsumingInput(string input, int offset)
    {
        var context = new ParseContext(new Scanner(input));
        var cursor = context.Scanner.Cursor;
        cursor.Advance(offset);
        var start = cursor.Position;
        var result = new ParseResult<object?>();

        Assert.True(Eof().Parse(context, ref result));
        Assert.Equal(start, cursor.Position);
        Assert.Equal(offset, result.Start);
        Assert.Equal(offset, result.End);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("a", 0)]
    [InlineData(" ", 0)]
    [InlineData("42a", 2)]
    [InlineData("42 ", 2)]
    public void StandaloneEofFailsWithoutConsumingInput(string input, int offset)
    {
        var context = new ParseContext(new Scanner(input));
        var cursor = context.Scanner.Cursor;
        cursor.Advance(offset);
        var start = cursor.Position;
        var sentinel = new object();
        var result = new ParseResult<object?>(7, 8, sentinel);

        Assert.False(Eof().Parse(context, ref result));
        Assert.Equal(start, cursor.Position);
        Assert.Equal(7, result.Start);
        Assert.Equal(8, result.End);
        Assert.Same(sentinel, result.Value);
    }

    [Fact]
    public void StandaloneEofReportsTheTrailingInputPosition()
    {
        var parser = Terms.Integer().AndSkip(Eof().ElseError("Unexpected trailing input"));

        Assert.True(parser.TryParse("42", out var value));
        Assert.Equal(42, value);
        var exception = Assert.Throws<ParseException>(() => parser.Parse("42a"));
        Assert.Equal("Unexpected trailing input", exception.Message);
        Assert.Equal(2, exception.Position.Offset);
        Assert.Equal(1, exception.Position.Line);
        Assert.Equal(3, exception.Position.Column);
    }

    [Fact]
    public void StandaloneEofSequenceRestoresTheCursorOnFailure()
    {
        var parser = Terms.Integer().AndSkip(Eof());
        var context = new ParseContext(new Scanner("42a"));
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<long>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
        Assert.True(parser.Or(Terms.Integer().AndSkip(Literals.Char('a'))).TryParse("42a", out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void GenericEofKeepsRollbackAndAlternativeSemantics()
    {
        var parser = Terms.Integer().Eof();
        var context = new ParseContext(new Scanner("42a"));
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<long>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
        var exception = Assert.Throws<ParseException>(() => parser.ElseError("Unexpected trailing input").Parse("42a"));
        Assert.Equal(start, exception.Position);
        Assert.True(parser.Or(Terms.Integer().AndSkip(Literals.Char('a'))).TryParse("42a", out var value));
        Assert.Equal(42, value);
    }
}
