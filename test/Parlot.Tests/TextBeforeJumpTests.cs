using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

// AnyCharBefore searches the text for the chars its delimiter can start with. The result must be the one
// obtained by trying the delimiter at each position, which is what a delimiter that isn't seekable gets.
public class TextBeforeJumpTests
{
    // Hides the seekable properties of a parser
    private static Parser<T> NotSeekable<T>(Parser<T> parser)
    {
        var deferred = Deferred<T>();
        deferred.Parser = parser;

        Assert.False(((ISeekable)deferred).CanSeek);

        return deferred;
    }

    [Theory]
    [InlineData("abc!def", "abc")]
    [InlineData("abc--def", "abc")]
    [InlineData("a-b!c--d", "a-b")]
    [InlineData("abcdef", "abcdef")]
    public void ShouldFindADelimiterWhichIsNotSeekableInAOneOf(string text, string expected)
    {
        var delimiter = Literals.Text("--").Or(Literals.Pattern(static c => c == '!').Then(static x => x.ToString()));

        // The pattern can start with any char
        Assert.Contains(Parlot.Fluent.OneOf<string>.OtherSeekableChar, ((ISeekable)delimiter).ExpectedChars);

        Assert.True(AnyCharBefore(delimiter).TryParse(text, out var result));
        Assert.Equal(expected, result.ToString());

        Assert.True(AnyCharBefore(NotSeekable(delimiter)).TryParse(text, out result));
        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("abc   end", "abc")]
    [InlineData("abc\n end", "abc")]
    [InlineData("abcend", "abc")]
    [InlineData("abc   en", "abc   en")]
    public void ShouldStopBeforeTheWhiteSpacesOfTheDelimiter(string text, string expected)
    {
        var delimiter = Terms.Text("end");

        Assert.True(AnyCharBefore(delimiter).TryParse(text, out var result));
        Assert.Equal(expected, result.ToString());

        Assert.True(AnyCharBefore(NotSeekable(delimiter)).TryParse(text, out result));
        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("abc-->def", "abc")]
    [InlineData("a-b->c-->d", "a-b->c")]
    [InlineData("a==>b", "a")]
    [InlineData("abc", "abc")]
    public void ShouldStillFindASeekableDelimiter(string text, string expected)
    {
        var delimiter = Literals.Text("-->").Or(Literals.Text("==>"));

        Assert.True(AnyCharBefore(delimiter).TryParse(text, out var result));
        Assert.Equal(expected, result.ToString());

        Assert.True(AnyCharBefore(NotSeekable(delimiter)).TryParse(text, out result));
        Assert.Equal(expected, result.ToString());
    }
}
