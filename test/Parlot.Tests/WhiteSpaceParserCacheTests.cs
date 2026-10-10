using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

// The context remembers the white spaces skipped at an offset. They depend on the white space parser,
// so they must not be reused once WithWhiteSpaceParser or WithComments changed it.
public class WhiteSpaceParserCacheTests
{
    private static readonly Parser<TextSpan> _spacesOnly = Literals.WhiteSpace(includeNewLines: false);

    [Fact]
    public void CustomWhiteSpaceParserShouldNotReuseTheDefaultWhiteSpaces()
    {
        var custom = Terms.Text("b").WithWhiteSpaceParser(_spacesOnly);

        // The new line is not a white space for the custom parser
        Assert.False(custom.TryParse("\n b", out _));

        // 'a' skips the new line with the default white spaces, at the offset the custom parser then starts from.
        // The literal prevents the white spaces from being skipped once for all the alternatives.
        var parser = OneOf(Literals.Text("zzz"), Terms.Text("a"), custom);

        Assert.False(parser.TryParse("\n b", out _));
        Assert.True(parser.TryParse("  b", out var value));
        Assert.Equal("b", value);
    }

    [Fact]
    public void DefaultWhiteSpacesShouldNotReuseTheCustomWhiteSpaceParser()
    {
        const string text = " # comment\n b";

        // The comment is not a white space by default
        Assert.False(Terms.Text("b").TryParse(text, out _));

        var comments = Terms.Text("a").WithComments(static c =>
        {
            c.WithWhiteSpaceOrNewLine();
            c.WithSingleLine("#");
        });

        // 'a' skips the comment with its own white space parser, at the offset 'b' then starts from
        var parser = OneOf(Literals.Text("zzz"), comments, Terms.Text("b"));

        Assert.False(parser.TryParse(text, out _));
        Assert.True(parser.TryParse("  b", out var value));
        Assert.Equal("b", value);
    }

    [Fact]
    public void OneOfShouldNotSkipWhiteSpacesForACustomWhiteSpaceParser()
    {
        var parser = OneOf(
            Terms.Text("a").WithWhiteSpaceParser(_spacesOnly),
            Terms.Text("b").WithWhiteSpaceParser(_spacesOnly));

        Assert.False(parser.TryParse("\n a", out _));
        Assert.False(parser.TryParse("\n b", out _));

        Assert.True(parser.TryParse("  a", out var value));
        Assert.Equal("a", value);

        Assert.True(parser.TryParse("  b", out value));
        Assert.Equal("b", value);
    }

    [Fact]
    public void SettingTheWhiteSpaceParserShouldForgetTheSkippedWhiteSpaces()
    {
        var context = new ParseContext(new Scanner("\n b"));

        context.SkipWhiteSpace();
        Assert.Equal(2, context.Scanner.Cursor.Offset);

        context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        context.WhiteSpaceParser = _spacesOnly;

        context.SkipWhiteSpace();
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        context.WhiteSpaceParser = null;

        context.SkipWhiteSpace();
        Assert.Equal(2, context.Scanner.Cursor.Offset);
    }
}
