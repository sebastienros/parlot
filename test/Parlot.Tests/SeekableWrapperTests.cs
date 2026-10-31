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
        var parser = Terms.Text("a").Switch(static (_, _) => 0, Literals.Text("x"), Literals.Text("y"));
        var seekable = Seekable(parser);

        Assert.True(seekable.CanSeek);
        Assert.Equal(['a'], seekable.ExpectedChars);
        Assert.True(seekable.SkipWhitespace);

        Assert.False(Seekable(Literals.Identifier(static c => c == '-').Switch(static (_, _) => 0, Literals.Text("x"))).CanSeek);

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

    [Fact]
    public void IfShouldDeclareTheUnionOfItsBranches()
    {
        var guard = Seekable(If(static () => true, Terms.Text("a")));

        Assert.True(guard.CanSeek);
        Assert.Equal(['a'], guard.ExpectedChars);
        Assert.True(guard.SkipWhitespace);

        var branches = Seekable(If(static () => true, Terms.Text("a"), Terms.Text("b")));

        Assert.True(branches.CanSeek);
        Assert.Equal(['a', 'b'], branches.ExpectedChars);
        Assert.True(branches.SkipWhitespace);

        var contextual = Seekable(If(static (ParseContext _) => true, Literals.Text("a"), Literals.Text("ab")));

        Assert.True(contextual.CanSeek);
        Assert.Equal(['a'], contextual.ExpectedChars);
        Assert.False(contextual.SkipWhitespace);

        // Branches that disagree on white space, or can't seek, have no common hint
        Assert.False(Seekable(If(static () => true, Terms.Text("a"), Literals.Text("b"))).CanSeek);
        Assert.False(Seekable(If(static () => true, Terms.Text("a"), Terms.Identifier(static c => c == '-').Then(static x => x.ToString()))).CanSeek);
        Assert.False(Seekable(If(static () => true, Terms.Identifier(static c => c == '-'))).CanSeek);
    }

    [Fact]
    public void SelectShouldDeclareTheUnionOfItsBranches()
    {
        var select = Seekable(Select(static () => 0, Terms.Text("a"), Terms.Text("b"), Terms.Text("c")));

        Assert.True(select.CanSeek);
        Assert.Equal(['a', 'b', 'c'], select.ExpectedChars);
        Assert.True(select.SkipWhitespace);

        var contextual = Seekable(Select(static (ParseContext _) => 0, Literals.Text("a"), Literals.Text("ab")));

        Assert.True(contextual.CanSeek);
        Assert.Equal(['a'], contextual.ExpectedChars);
        Assert.False(contextual.SkipWhitespace);

        Assert.False(Seekable(Select(static () => 0, Terms.Text("a"), Literals.Text("b"))).CanSeek);
        Assert.False(Seekable(Select(static () => 0, Terms.Text("a"), Terms.Identifier(static c => c == '-').Then(static x => x.ToString()))).CanSeek);
        Assert.False(Seekable(Select<string>(static () => 0)).CanSeek);
    }

    [Theory]
    [InlineData(0, " a", "a")]
    [InlineData(1, " b", "b")]
    [InlineData(0, " b", null)]
    [InlineData(1, " a", null)]
    [InlineData(0, " z", "z")]
    [InlineData(1, " z", "z")]
    [InlineData(0, " c", null)]
    public void ChoiceShouldOnlyTrySelectForTheCharsOfItsBranches(int index, string input, string expected)
    {
        var evaluations = 0;
        var select = Select(() => { evaluations++; return index; }, Terms.Text("a"), Terms.Text("b"));
        var choice = OneOf(select, Terms.Text("z"));

        Assert.Equal(['a', 'b', 'z'], Seekable(choice).ExpectedChars);
        Assert.True(Seekable(choice).SkipWhitespace);

        Assert.Equal(expected, choice.TryParse(input, out var result) ? result : null);

        // The selector only ran when the next char was one of the branches'
        Assert.Equal(input is " a" or " b" ? 1 : 0, evaluations);
    }
}
