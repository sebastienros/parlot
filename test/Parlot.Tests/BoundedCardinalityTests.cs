using Parlot.Fluent;
using System;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class BoundedCardinalityTests
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void ZeroOrManyStopsAtMaximum(int max, int count)
    {
        var parser = ZeroOrMany(Literals.Char('x'), max);
        var context = new ParseContext(new Scanner("xxx!"));
        var result = new ParseResult<System.Collections.Generic.IReadOnlyList<char>>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(count, result.Value.Count);
        Assert.Equal(count, context.Scanner.Cursor.Offset);
        Assert.Equal(count, Literals.Char('x').ZeroOrMany(max).Parse("xxx").Count);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void OneOrManyStopsAtMaximum(int max, int count)
    {
        var parser = OneOrMany(Literals.Char('x'), max);
        var context = new ParseContext(new Scanner("xxx!"));
        var result = new ParseResult<System.Collections.Generic.IReadOnlyList<char>>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(count, result.Value.Count);
        Assert.Equal(count, context.Scanner.Cursor.Offset);
        Assert.Equal(count, Literals.Char('x').OneOrMany(max).Parse("xxx").Count);
        Assert.False(parser.TryParse("", out _));
    }

    [Theory]
    [InlineData(0, 3, 5)]
    [InlineData(1, 3, 5)]
    [InlineData(1, 2, 3)]
    [InlineData(0, 1, 1)]
    public void SeparatedStopsAtMaximum(int min, int max, int expectedOffset)
    {
        var parser = Separated(Literals.Char(','), Literals.Char('x'), min, max);
        var context = new ParseContext(new Scanner("x,x,x!"));
        var result = new ParseResult<System.Collections.Generic.IReadOnlyList<char>>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(expectedOffset, context.Scanner.Cursor.Offset);
        Assert.Equal((expectedOffset + 1) / 2, result.Value.Count);
    }

    [Fact]
    public void SeparatedAllowsEmptyOnlyWithExplicitMinimum()
    {
        Assert.False(Separated(Literals.Char(','), Literals.Char('x')).TryParse("", out _));
        Assert.True(Separated(Literals.Char(','), Literals.Char('x'), min: 0).TryParse("", out var empty));
        Assert.Empty(empty);
        Assert.True(Separated(Literals.Char(','), Literals.Char('x'), min: 0, max: 2).TryParse("", out empty));
        Assert.Empty(empty);
        Assert.True(Separated(Literals.Char(','), Literals.Char('x'), max: 2).TryParse("x,x", out var values));
        Assert.Equal(2, values.Count);
        Assert.True(OneOf(Separated(Literals.Char(','), Literals.Char('x'), min: 0).Then('x'), Literals.Char('y')).TryParse("z", out _));
    }

    [Fact]
    public void SeparatedRestoresCursorWhenMinimumIsNotMet()
    {
        var parser = Separated(Literals.Char(','), Literals.Char('x'), min: 3, max: 4);
        var context = new ParseContext(new Scanner("x,x!"));
        var result = new ParseResult<System.Collections.Generic.IReadOnlyList<char>>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
        Assert.True(parser.Or(Literals.Char('x').ZeroOrMany()).TryParse("x,x!", out var fallback));
        Assert.Single(fallback);
        Assert.False(Separated(Literals.Char(','), Literals.Char('x'), min: 3).TryParse("x,x,", out _));
        Assert.True(Separated(Literals.Char(','), Literals.Char('x'), min: 2).AndSkip(Literals.Char(',')).TryParse("x,x,", out _));
    }

    [Fact]
    public void BoundedParsersLeaveExcessInputForContinuation()
    {
        Assert.True(ZeroOrMany(Literals.Char('x'), max: 2).And(Literals.Char('x')).Eof().TryParse("xxx", out _));
        Assert.True(OneOrMany(Literals.Char('x'), max: 2).And(Literals.Char('x')).Eof().TryParse("xxx", out _));
        Assert.True(Separated(Literals.Char(','), Literals.Char('x'), max: 2).AndSkip(Literals.Char(',')).And(Literals.Char('x')).Eof().TryParse("x,x,x", out _));
        Assert.False(ZeroOrMany(Literals.Char('x'), max: 2).Eof().TryParse("xxx", out _));
    }

    [Fact]
    public void InvalidBoundsThrowAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ZeroOrMany(Literals.Char('x'), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => OneOrMany(Literals.Char('x'), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Separated(Literals.Char(','), Literals.Char('x'), min: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Separated(Literals.Char(','), Literals.Char('x'), max: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Separated(Literals.Char(','), Literals.Char('x'), min: 3, max: 2));
    }
}
