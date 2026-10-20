using System.Linq;
using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// Parsers that run another parser first declare what that parser can start with, so a OneOf can skip them.
/// </summary>
public class SeekableWrapperTests
{
    private static ISeekable Seekable<T>(Parser<T> parser) => Assert.IsAssignableFrom<ISeekable>(parser);

    [Fact]
    public void EofShouldForwardTheHints()
    {
        var terms = Seekable(Terms.Text("a").Eof());

        Assert.True(terms.CanSeek);
        Assert.Equal(['a'], terms.ExpectedChars);
        Assert.True(terms.SkipWhitespace);

        var literals = Seekable(Literals.Text("a").Eof());

        Assert.True(literals.CanSeek);
        Assert.Equal(['a'], literals.ExpectedChars);
        Assert.False(literals.SkipWhitespace);

        Assert.False(Seekable(Literals.Identifier(static c => c == '-').Eof()).CanSeek);

        // The choice doesn't try it for other chars
        var choice = OneOf(Terms.Text("a").Eof(), Terms.Text("b"));

        Assert.Equal(['a', 'b'], Seekable(choice).ExpectedChars.OrderBy(static c => c));
        Assert.Equal("a", choice.Parse(" a"));
        Assert.Equal("b", choice.Parse(" b"));
        Assert.False(choice.TryParse(" a ", out _));
        Assert.False(choice.TryParse(" c", out _));
    }

    [Fact]
    public void SwitchShouldForwardTheHintsOfThePreviousParser()
    {
        var x = Literals.Text("x");
        var parser = Terms.Text("a").Switch((_, _) => x);
        var seekable = Seekable(parser);

        Assert.True(seekable.CanSeek);
        Assert.Equal(['a'], seekable.ExpectedChars);
        Assert.True(seekable.SkipWhitespace);

        Assert.False(Seekable(Literals.Identifier(static c => c == '-').Switch((_, _) => x)).CanSeek);

        var choice = OneOf(parser, Terms.Text("b"));

        Assert.Equal(['a', 'b'], Seekable(choice).ExpectedChars.OrderBy(static c => c));
        Assert.Equal("x", choice.Parse(" ax"));
        Assert.Equal("b", choice.Parse(" b"));
        Assert.False(choice.TryParse(" x", out _));
    }

    [Fact]
    public void ErrorShouldForwardTheHints()
    {
        var seekable = Seekable(Terms.Text("a").Error("no a"));

        Assert.True(seekable.CanSeek);
        Assert.Equal(['a'], seekable.ExpectedChars);
        Assert.True(seekable.SkipWhitespace);

        var converted = Seekable(Terms.Text("a").Error<int>("no a"));

        Assert.True(converted.CanSeek);
        Assert.Equal(['a'], converted.ExpectedChars);
        Assert.True(converted.SkipWhitespace);

        Assert.False(Seekable(Literals.Identifier(static c => c == '-').Error("no identifier")).CanSeek);

        // The error is only raised when the parser is tried, which the lookup table decides
        var choice = OneOf(Terms.Text("a").Error("no a"), Terms.Text("b"));

        Assert.Equal(['a', 'b'], Seekable(choice).ExpectedChars.OrderBy(static c => c));
        Assert.Equal("b", choice.Parse(" b"));
        Assert.False(choice.TryParse(" c", out _));
        Assert.Equal("no a", Assert.Throws<ParseException>(() => choice.Parse(" a")).Message);
    }
}
