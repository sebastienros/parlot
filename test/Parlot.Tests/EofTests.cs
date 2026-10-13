#nullable enable

using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class EofTests
{
    [Theory]
    [InlineData(false, "", 0)]
    [InlineData(true, "", 0)]
    [InlineData(false, "42", 2)]
    [InlineData(true, "42", 2)]
    [InlineData(false, "a\n", 2)]
    [InlineData(true, "a\n", 2)]
    public void StandaloneEofSucceedsWithoutConsumingInput(bool compiled, string input, int offset)
    {
        var parser = compiled ? Eof().Compile() : Eof();
        var context = new ParseContext(new Scanner(input));
        var cursor = context.Scanner.Cursor;
        cursor.Advance(offset);
        var start = cursor.Position;
        var result = new ParseResult<object?>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(start, cursor.Position);
        Assert.Equal(offset, result.Start);
        Assert.Equal(offset, result.End);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(false, "a", 0)]
    [InlineData(true, "a", 0)]
    [InlineData(false, " ", 0)]
    [InlineData(true, " ", 0)]
    [InlineData(false, "42a", 2)]
    [InlineData(true, "42a", 2)]
    [InlineData(false, "42 ", 2)]
    [InlineData(true, "42 ", 2)]
    public void StandaloneEofFailsWithoutConsumingInput(bool compiled, string input, int offset)
    {
        var parser = compiled ? Eof().Compile() : Eof();
        var context = new ParseContext(new Scanner(input));
        var cursor = context.Scanner.Cursor;
        cursor.Advance(offset);
        var start = cursor.Position;
        var sentinel = new object();
        var result = new ParseResult<object?>(7, 8, sentinel);

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, cursor.Position);
        Assert.Equal(7, result.Start);
        Assert.Equal(8, result.End);
        Assert.Same(sentinel, result.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandaloneEofReportsTheTrailingInputPosition(bool compiled)
    {
        Parser<long> parser = Terms.Integer().AndSkip(Eof().ElseError("Unexpected trailing input"));
        if (compiled) parser = parser.Compile();

        Assert.True(parser.TryParse("42", out var value));
        Assert.Equal(42, value);
        var exception = Assert.Throws<ParseException>(() => parser.Parse("42a"));
        Assert.Equal("Unexpected trailing input", exception.Message);
        Assert.Equal(2, exception.Position.Offset);
        Assert.Equal(1, exception.Position.Line);
        Assert.Equal(3, exception.Position.Column);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandaloneEofSequenceRestoresTheCursorOnFailure(bool compiled)
    {
        Parser<long> parser = Terms.Integer().AndSkip(Eof());
        var alternative = parser.Or(Terms.Integer().AndSkip(Literals.Char('a')));
        if (compiled)
        {
            parser = parser.Compile();
            alternative = alternative.Compile();
        }
        var context = new ParseContext(new Scanner("42a"));
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<long>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
        Assert.True(alternative.TryParse("42a", out var value));
        Assert.Equal(42, value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericEofKeepsRollbackAndAlternativeSemantics(bool compiled)
    {
        var parser = Terms.Integer().Eof();
        var errorParser = parser.ElseError("Unexpected trailing input");
        var alternative = parser.Or(Terms.Integer().AndSkip(Literals.Char('a')));
        if (compiled)
        {
            parser = parser.Compile();
            errorParser = errorParser.Compile();
            alternative = alternative.Compile();
        }
        var context = new ParseContext(new Scanner("42a"));
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<long>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
        var exception = Assert.Throws<ParseException>(() => errorParser.Parse("42a"));
        Assert.Equal("Unexpected trailing input", exception.Message);
        Assert.Equal(start, exception.Position);
        Assert.True(alternative.TryParse("42a", out var value));
        Assert.Equal(42, value);
    }
}
