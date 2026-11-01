using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// A choice of a single alternative declares the hints of that alternative, so the choices which hold it can skip it.
/// </summary>
public class OneOfSingleAlternativeTests
{
    [Fact]
    public void ShouldDeclareTheHintsOfItsAlternative()
    {
        var seekable = (ISeekable)new OneOf<char>([Terms.Char('a')]);

        Assert.True(seekable.CanSeek);
        Assert.True(seekable.SkipWhitespace);
        Assert.Equal(['a'], seekable.ExpectedChars);
    }

    [Theory]
    [InlineData("a", true, 1)]
    [InlineData("  a", true, 3)]
    [InlineData("  b", false, 0)]
    [InlineData("", false, 0)]
    public void ShouldParseLikeItsAlternative(string input, bool success, int offset)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<char>();

        Assert.Equal(success, new OneOf<char>([Terms.Char('a')]).Parse(context, ref result));
        Assert.Equal(offset, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldNotSeekWhenItsAlternativeCannot()
    {
        var seekable = (ISeekable)new OneOf<string>([Literals.Pattern(static c => c != ',').Then(static x => x.ToString())]);

        Assert.False(seekable.CanSeek);
        Assert.Equal("ab", new OneOf<string>([Literals.Pattern(static c => c != ',').Then(static x => x.ToString())]).Parse("ab,c"));
    }

    [Fact]
    public void EnclosingChoiceShouldSeekThroughIt()
    {
        var single = new OneOf<char>([Terms.Char('a')]);
        var choice = (ISeekable)new OneOf<char>([single, Terms.Char('b')]);

        Assert.True(choice.CanSeek);
        Assert.Contains('a', choice.ExpectedChars);
        Assert.Contains('b', choice.ExpectedChars);
        Assert.Equal('a', ((Parser<char>)choice).Parse(" a"));
        Assert.Equal('b', ((Parser<char>)choice).Parse(" b"));
    }

    [Fact]
    public void ShouldDeclareTheHintsOfADeferredAlternativeSetLater()
    {
        var deferred = Deferred<char>();
        var single = new OneOf<char>([deferred]);
        deferred.Parser = Terms.Char('x');

        Assert.Equal('x', single.Parse(" x"));
        Assert.True(single.CanSeek);
        Assert.Equal(['x'], single.ExpectedChars);
    }
}
