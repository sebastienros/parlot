#nullable enable

using Parlot.Fluent;
using Parlot.Rewriting;
using System.Collections.Generic;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// <see cref="Parsers.Separated{U, T}(Parser{U}, Parser{T}, Parser{T}, int, bool, bool)"/>: a list which can end with a
/// last element of another kind, which no separator follows (#588).
/// </summary>
public class SeparatedLastTests
{
    private static readonly Parser<string> Item = Terms.Identifier().Then(static x => x.ToString()!);
    private static readonly Parser<string> Rest = Terms.Text("...").SkipAnd(Terms.Identifier()).Then(static x => "..." + x.ToString());

    private static Parser<IReadOnlyList<string>> List(int min = 0, bool allowTrailingSeparator = false, bool removeEmptyEntries = false) =>
        Separated(Terms.Char(','), Item, Rest, min, allowTrailingSeparator, removeEmptyEntries);

    private static (bool Success, string Values, int Start, int End, int Offset) Run(Parser<IReadOnlyList<string>> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<IReadOnlyList<string>>();
        var success = parser.Parse(context, ref result);

        return (success, success ? string.Join("|", result.Value) : "", result.Start, result.End, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("", "", 0)]
    [InlineData("...r", "...r", 4)]
    [InlineData(" ...r", "...r", 5)]
    [InlineData("a", "a", 1)]
    [InlineData("a, b", "a|b", 4)]
    [InlineData("a, b, ...r", "a|b|...r", 10)]
    // No separator is consumed after the last element: the list ends there
    [InlineData("a, ...r, b", "a|...r", 7)]
    [InlineData("...r, b", "...r", 4)]
    [InlineData("...r,", "...r", 4)]
    // A separator which no element follows isn't consumed
    [InlineData("a, b,", "a|b", 4)]
    [InlineData("a, b, )", "a|b", 4)]
    [InlineData("a, ...", "a", 1)]
    // A separator can't lead the list
    [InlineData(", a", "", 0)]
    [InlineData(")", "", 0)]
    public void ShouldParseItemsAndLast(string text, string values, int offset)
    {
        var (success, actual, _, end, cursor) = Run(List(), text);

        Assert.True(success);
        Assert.Equal(values, actual);
        Assert.Equal(offset, cursor);
        Assert.Equal(offset, end);
    }

    [Fact]
    public void ShouldStartAtTheFirstElement()
    {
        Assert.Equal((true, "a|...r", 2, 9, 9), Run(List(), "  a, ...r"));
        Assert.Equal((true, "...r", 1, 5, 5), Run(List(), " ...r"));
        Assert.Equal((true, "", 0, 0, 0), Run(List(), "  )"));
    }

    [Theory]
    [InlineData("a, b,", "a|b", 5)]
    [InlineData("a, b, )", "a|b", 5)]
    // The trailing separator is consumed after the items of the parser, never after the last element
    [InlineData("a, ...r,", "a|...r", 7)]
    [InlineData("...r,", "...r", 4)]
    [InlineData(", a", "", 0)]
    public void ShouldConsumeTrailingSeparatorAfterItems(string text, string values, int offset)
    {
        var (success, actual, _, _, cursor) = Run(List(allowTrailingSeparator: true), text);

        Assert.True(success);
        Assert.Equal(values, actual);
        Assert.Equal(offset, cursor);
    }

    [Theory]
    [InlineData("a, , b", "a|b", 6)]
    [InlineData("a, , ...r", "a|...r", 9)]
    [InlineData("a,,,...r, b", "a|...r", 8)]
    [InlineData("a, ,", "a", 1)]
    [InlineData(", a", "", 0)]
    public void ShouldRemoveEmptyEntriesBeforeLast(string text, string values, int offset)
    {
        var (success, actual, _, _, cursor) = Run(List(removeEmptyEntries: true), text);

        Assert.True(success);
        Assert.Equal(values, actual);
        Assert.Equal(offset, cursor);
    }

    [Fact]
    public void ShouldConsumeTrailingSeparatorsWithRemoveEmptyEntries()
    {
        Assert.Equal((true, "a", 0, 4, 4), Run(List(allowTrailingSeparator: true, removeEmptyEntries: true), "a, ,"));
    }

    [Theory]
    [InlineData("", false, "")]
    [InlineData(")", false, "")]
    [InlineData("a", false, "")]
    [InlineData("...r", false, "")]
    [InlineData("a, b", true, "a|b")]
    [InlineData("a, ...r", true, "a|...r")]
    [InlineData("a, b, ...r", true, "a|b|...r")]
    [InlineData("a, ...", false, "")]
    [InlineData("...r, a", false, "")]
    public void ShouldCountTheLastElementInMin(string text, bool success, string values)
    {
        var (actualSuccess, actual, _, _, cursor) = Run(List(min: 2), text);

        Assert.Equal(success, actualSuccess);
        Assert.Equal(values, actual);

        if (!success)
        {
            // The cursor is restored on failure
            Assert.Equal(0, cursor);
        }
    }

    [Fact]
    public void ShouldFailWithoutElementWhenMinIsSet()
    {
        Assert.Equal((false, "", 0, 0, 0), Run(List(min: 1), "  , a"));
        Assert.Equal((false, "", 0, 0, 0), Run(List(min: 1), "  ... "));
        Assert.Equal((true, "...r", 0, 4, 4), Run(List(min: 1), "...r"));
    }

    [Fact]
    public void ShouldRestoreTheCursorForFollowingParsers()
    {
        var parser = List().AndSkip(Terms.Char(',')).AndSkip(Terms.Char(')')).Then(static x => string.Join("|", x));

        Assert.Equal("a|b", parser.Parse("a, b, )"));
        Assert.Equal("a|...r", parser.Parse("a, ...r, )"));
        Assert.Null(parser.Parse("a, ...r )"));

        // The trailing separator is consumed after the items, so it isn't left for the following parsers
        Assert.Null(List(allowTrailingSeparator: true).AndSkip(Terms.Char(',')).Parse("a, b, )"));
    }

    [Fact]
    public void ShouldPreferTheParserToTheLastElement()
    {
        // The last element is only tried where the parser doesn't match
        var parser = Separated(Terms.Char(','), Terms.Identifier().Then(static x => x.ToString()!), Terms.Identifier().Then(static x => "last " + x.ToString()), min: 0);

        Assert.Equal(["a", "b"], parser.Parse("a, b"));
    }

    [Fact]
    public void ShouldIgnoreElementsWhichDoNotRead()
    {
        // An element which matches without reading isn't found, the last element is then tried
        var parser = Separated(Terms.Char(','), Terms.Identifier().Then(static x => x.ToString()!).Else("empty"), Rest, min: 0);

        Assert.Equal(["a", "...r"], parser.Parse("a, ...r"));
        Assert.Equal(["a"], parser.Parse("a, )"));
        Assert.Equal([], parser.Parse(")"));
    }

    [Fact]
    public void ShouldSeekTheFirstCharsOfBothElements()
    {
        var seekable = (ISeekable)List(min: 1);

        Assert.True(seekable.CanSeek);
        Assert.True(seekable.SkipWhitespace);
        Assert.Contains('.', seekable.ExpectedChars);
        Assert.Contains('a', seekable.ExpectedChars);

        // An empty list matches without reading
        Assert.False(((ISeekable)List()).CanSeek);

        // The table of a choice invokes the list for both
        var choice = OneOf(List(min: 1).Then(static x => string.Join("|", x)), Terms.Integer().Then(static x => x.ToString()));

        Assert.Equal("...r", choice.Parse("...r"));
        Assert.Equal("a|...r", choice.Parse("a, ...r"));
        Assert.Equal("12", choice.Parse("12"));
    }

    [Fact]
    public void ShouldForwardThePrefixesOfBothElements()
    {
        // With three alternatives starting with '.', the choice selects them by the prefixes they tell
        var list = Separated(Literals.Char(','), Literals.Text(".a"), Literals.Text("...").SkipAnd(Literals.Text("r")), min: 1).Then(static x => string.Join("|", x));
        var choice = OneOf(Literals.Text(".."), list, Literals.Text(".b"));

        Assert.Equal(".a|r", choice.Parse(".a,...r"));
        Assert.Equal("..", choice.Parse("...r"));
        Assert.Equal(".b", choice.Parse(".b"));

        var reordered = OneOf(Literals.Text(".b"), list, Literals.Text(".."));

        Assert.Equal("r", reordered.Parse("...r"));
        Assert.Equal("..", reordered.Parse("..x"));
        Assert.Equal(".a|.a", reordered.Parse(".a,.a"));
    }

    [Fact]
    public void ShouldNotInvokeTheLastElementWhereItCanNotStart()
    {
        var entered = 0;
        var list = List();
        var context = new ParseContext(new Scanner("a, )"))
        {
            OnEnterParser = (parser, _) => entered += ReferenceEquals(parser, Rest) ? 1 : 0,
        };
        var result = new ParseResult<IReadOnlyList<string>>();

        Assert.True(list.Parse(context, ref result));
        Assert.Equal(["a"], result.Value);
        Assert.Equal(1, context.Scanner.Cursor.Offset);
        Assert.Equal(0, entered);

        context = new ParseContext(new Scanner("a, ...r"))
        {
            OnEnterParser = (parser, _) => entered += ReferenceEquals(parser, Rest) ? 1 : 0,
        };

        Assert.True(list.Parse(context, ref result));
        Assert.Equal(1, entered);
    }

    [Fact]
    public void ShouldParseWithoutValue()
    {
        var parser = Capture(List(min: 1)).AndSkip(Terms.Char(';'));

        Assert.Equal("a, b, ...r", parser.Parse("a, b, ...r;").ToString());
        Assert.Equal(" ...r", parser.Parse(" ...r ;").ToString());
        Assert.Equal(default, parser.Parse("a, ...r, b;"));
    }

    [Fact]
    public void ShouldReportTheLastElement()
    {
        Assert.Equal($"Separated({Terms.Char(',')}, {Item}, {Rest})", List().ToString());
    }

    [Fact]
    public void ShouldCheckArguments()
    {
        Assert.Throws<System.ArgumentNullException>(() => Separated(Terms.Char(','), Item, null!));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => Separated(Terms.Char(','), Item, Rest, min: -1));
    }
}
