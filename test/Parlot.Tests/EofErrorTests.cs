using Parlot.Fluent;
using System.IO;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class EofErrorTests
{
    [Theory]
    [InlineData("42a", 2, 1, 3)]
    [InlineData(" 42a", 3, 1, 4)]
    [InlineData("\n42a", 3, 2, 3)]
    [InlineData("a", 0, 1, 1)]
    public void ElseErrorShouldReportTheEofFailurePosition(string text, int offset, int line, int column)
    {
        var parser = Terms.Integer().Eof().ElseError("Invalid token in expression");

        Assert.False(parser.TryParse(text, out _, out var error));
        Assert.NotNull(error);
        Assert.Equal("Invalid token in expression", error.Message);
        Assert.Equal(offset, error.Position.Offset);
        Assert.Equal(line, error.Position.Line);
        Assert.Equal(column, error.Position.Column);
    }

    [Fact]
    public void ElseErrorShouldAcceptValidInput()
    {
        var parser = Terms.Integer().Eof().ElseError("Invalid token in expression");

        Assert.True(parser.TryParse("42", out var value, out var error));
        Assert.Equal(42, value);
        Assert.Null(error);
    }

    [Fact]
    public void ElseErrorShouldStopParsingInsteadOfTryingAnAlternative()
    {
        var parser = Terms.Integer().Eof().ElseError("Invalid token in expression").Or(Terms.Integer());

        Assert.False(parser.TryParse("42a", out _, out var error));
        Assert.NotNull(error);
        Assert.Equal(2, error.Position.Offset);
    }

    [Theory]
    [InlineData("42a", 2, 1, 3)]
    [InlineData(" 42a", 3, 1, 4)]
    [InlineData("a", 0, 1, 1)]
    public void ElseErrorShouldReportTheEofFailurePositionFromAReader(string text, int offset, int line, int column)
    {
        var parser = Terms.Integer().Eof().ElseError("Invalid token in expression");
        using var reader = new StringReader(text);

        Assert.False(parser.TryParse(reader, out _, out var error, new StreamParseOptions { BufferSize = 1 }));
        Assert.NotNull(error);
        Assert.Equal("Invalid token in expression", error.Message);
        Assert.Equal(offset, error.Position.Offset);
        Assert.Equal(line, error.Position.Line);
        Assert.Equal(column, error.Position.Column);
    }

    [Fact]
    public void EofShouldRestoreTheCursorAndAllowFallback()
    {
        var integer = Terms.Integer();
        var context = new ParseContext(new Scanner("42a"));
        var result = new ParseResult<long>();

        Assert.False(integer.Eof().Parse(context, ref result));
        Assert.Equal(TextPosition.Start, context.Scanner.Cursor.Position);
        Assert.True(integer.Eof().Or(integer).TryParse("42a", out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void SuccessfulFallbackShouldNotLeaveAStaleEofErrorPosition()
    {
        var parser = Terms.Integer().Eof().Or(Terms.Integer())
            .AndSkip(Literals.Char('b').ElseError("Expected b"));

        Assert.False(parser.TryParse("42a", out _, out var error));
        Assert.NotNull(error);
        Assert.Equal("Expected b", error.Message);
        Assert.Equal(2, error.Position.Offset);
    }

    [Fact]
    public void ElseErrorShouldNotChangeASharedEofParser()
    {
        var eof = Terms.Integer().Eof();
        var errorParser = new ElseError<long>(eof, "Invalid token in expression");

        Assert.False(errorParser.TryParse("42a", out _, out var error));
        Assert.NotNull(error);
        Assert.Equal(2, error.Position.Offset);
        Assert.False(eof.TryParse("42a", out _, out error));
        Assert.Null(error);
        Assert.True(eof.Or(Terms.Integer()).TryParse("42a", out var value));
        Assert.Equal(42, value);
    }
}
