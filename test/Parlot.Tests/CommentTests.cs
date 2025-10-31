using Parlot.Fluent;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class CommentTests
{
    [Theory]
    [InlineData("-- single line comment", "-- single line comment")]
    [InlineData("-- ", "-- ")]
    [InlineData("--", "--")]
    [InlineData("--\n", "--")]
    [InlineData("--\r\n", "--")]
    [InlineData("-- some comment\n text here", "-- some comment")]
    public void ShouldReadSingleLineComments(string text, string expected)
    {
        var comments = Literals.Comments("--");
        Assert.Equal(expected, comments.Parse(text).ToString());
    }

    [Theory]
    [InlineData("hello-- single line comment\n world")]
    [InlineData("hello-- \n world")]
    [InlineData("hello--\n world")]
    [InlineData("hello  --\n world")]
    public void ShouldSkipSingleLineComments(string text)
    {

        var comments = Terms.Text("hello").And(Terms.Text("world")).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.WhiteSpace(includeNewLines: true).Or(Literals.Comments("--")))));
        Assert.True(comments.TryParse(text, out _));
    }

    [Theory]
    [InlineData("hello -- single line comment")]
    [InlineData("hello --")]
    [InlineData("hello--")]
    public void ShouldReadSingleLineCommentsAfterText(string text)
    {
        var comments = Terms.Text("hello").And(Terms.Comments("--"));
        Assert.True(comments.TryParse(text, out _));
    }

    [Theory]
    [InlineData("/* multi line comment */")]
    [InlineData("/* multi \nline comment */")]
    [InlineData("/**/")]
    [InlineData("/*\n*/")]
    [InlineData("/* */")]
    public void ShouldReadMultiLineComments(string text)
    {
        var comments = Literals.Comments("/*", "*/");
        Assert.Equal(text, comments.Parse(text).ToString());
    }

    [Theory]
    [InlineData("hello /* multi line comment */world")]
    [InlineData("hello /**/world")]
    [InlineData("hello/* */ world")]
    [InlineData("hello /* multi line \n comment */    world")]
    [InlineData("hello /* multi line \n comment */    world\n")]
    [InlineData("hello /* multi \nline \n comment */   world")]
    [InlineData("hello /* multi line \n\n comment */  world")]
    [InlineData("hello /*\n*/ world")]
    [InlineData("hello/* */ world\n")]
    public void ShouldReadMultiLineCommentsAfterText(string text)
    {
        var comments = Terms.Text("hello").And(Terms.Comments("/*", "*/")).And(Terms.Text("world"));
        Assert.True(comments.TryParse(text, out _));
    }

    [Theory]
    [InlineData("hello /* multi line comment ")]
    [InlineData("hello /* asd")]
    [InlineData("hello/* ")]
    public void ShouldFailUnterminatedMultiLineComments(string text)
    {
        var comments = Terms.Text("hello").And(Terms.Comments("/*", "*/"));
        Assert.False(comments.TryParse(text, out _));
    }

    [Theory]
    [InlineData("hello-- single line comment\n world")]
    [InlineData("hello-- \n world")]
    [InlineData("hello--\n world")]
    [InlineData("hello  --\n world")]
    [InlineData("hello  --\r\n world")]
    [InlineData("hello  --   \r\n world")]
    [InlineData("hello world # comment")]
    [InlineData("hello world -- comment")]
    [InlineData("hello world -- # comment")]
    [InlineData("hello#comment\nworld ")]
    [InlineData("hello\n#\n#\n--\r\nworld")]
    [InlineData("hello/* comment */ /*comment2*/ world")]
    [InlineData("hello/*--\n*/ world")]
    [InlineData("hello /* /* */world")]
    [InlineData("hello world")]
    public void ShouldParseAllComments(string text)
    {
        var comments = Terms.Text("hello").And(Terms.Text("world"))
            .WithComments(builder =>
            {
                builder.WithSingleLine("--");
                builder.WithSingleLine("#");
                builder.WithMultiLine("/*", "*/");
            });

        Assert.True(comments.TryParse(text, out _));
    }
}
