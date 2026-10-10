using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

// A parser that fails must leave the cursor where it started, such that the next alternative reads the same text.
public class CursorRestoredOnFailureTests
{
    private static void AssertFailsAtStart<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Position.Offset);
        Assert.Equal(1, context.Scanner.Cursor.Position.Line);
        Assert.Equal(1, context.Scanner.Cursor.Position.Column);
    }

    [Fact]
    public void EofShouldRestoreTheCursorWhenTextFollows()
    {
        AssertFailsAtStart(Literals.Text("ab").Eof(), "abc");
    }

    [Fact]
    public void EofShouldLetTheNextAlternativeMatch()
    {
        var parser = Literals.Text("ab").Eof().Or(Literals.Text("abc"));

        Assert.True(parser.TryParse("abc", out var value));
        Assert.Equal("abc", value);

        Assert.True(parser.TryParse("ab", out value));
        Assert.Equal("ab", value);
    }

    [Fact]
    public void SwitchShouldRestoreTheCursorWhenTheSelectedParserFails()
    {
        AssertFailsAtStart(Literals.Text("a").Switch(static (_, _) => 0, Literals.Text("b")), "ac");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void SwitchShouldRestoreTheCursorWhenTheIndexIsOutOfRange(int index)
    {
        AssertFailsAtStart(Literals.Text("a").Switch((_, _) => index, Literals.Text("b")), "ab");
    }

    [Fact]
    public void SwitchShouldLetTheNextAlternativeMatch()
    {
        var parser = Literals.Text("a").Switch(static (_, _) => 0, Literals.Text("b")).Or(Literals.Text("ac"));

        Assert.True(parser.TryParse("ac", out var value));
        Assert.Equal("ac", value);

        Assert.True(parser.TryParse("ab", out value));
        Assert.Equal("b", value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnyCharBeforeShouldRestoreTheCursorWhenEmpty(bool consumeDelimiter)
    {
        AssertFailsAtStart(AnyCharBefore(Literals.Char(','), consumeDelimiter: consumeDelimiter), ",abc");
    }

    [Fact]
    public void AnyCharBeforeShouldLetTheNextAlternativeMatch()
    {
        var parser = AnyCharBefore(Literals.Char(','), consumeDelimiter: true).Then(static x => x.ToString())
            .Or(Literals.Text(",abc"));

        Assert.True(parser.TryParse(",abc", out var value));
        Assert.Equal(",abc", value);
    }
}
