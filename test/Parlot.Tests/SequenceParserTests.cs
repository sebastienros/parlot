using Parlot.Fluent;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class SequenceParserTests
{
    [Fact]
    public void SequenceAndSkip5ShouldParseFiveElements()
    {
        var parser = Literals.Char('a')
            .And(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'))
            .AndSkip(Literals.Char('e'));

        Assert.True(parser.TryParse("abcde", out var result));
        Assert.Equal('a', result.Item1);
        Assert.Equal('b', result.Item2);
        Assert.Equal('c', result.Item3);
        Assert.Equal('d', result.Item4);
    }

    [Fact]
    public void SequenceAndSkip6ShouldParseSixElements()
    {
        var parser = Literals.Char('a')
            .And(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'))
            .And(Literals.Char('e'))
            .AndSkip(Literals.Char('f'));

        Assert.True(parser.TryParse("abcdef", out var result));
        Assert.Equal('a', result.Item1);
        Assert.Equal('b', result.Item2);
        Assert.Equal('c', result.Item3);
        Assert.Equal('d', result.Item4);
        Assert.Equal('e', result.Item5);
    }

    [Fact]
    public void SequenceAndSkip7ShouldParseSevenElements()
    {
        var parser = Literals.Char('a')
            .And(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'))
            .And(Literals.Char('e'))
            .And(Literals.Char('f'))
            .AndSkip(Literals.Char('g'));

        Assert.True(parser.TryParse("abcdefg", out var result));
        Assert.Equal('a', result.Item1);
        Assert.Equal('b', result.Item2);
        Assert.Equal('c', result.Item3);
        Assert.Equal('d', result.Item4);
        Assert.Equal('e', result.Item5);
        Assert.Equal('f', result.Item6);
    }

    [Fact]
    public void SequenceAndSkip8ShouldParseEightElements()
    {
        var parser = Literals.Char('a')
            .And(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'))
            .And(Literals.Char('e'))
            .And(Literals.Char('f'))
            .And(Literals.Char('g'))
            .AndSkip(Literals.Char('h'));

        Assert.True(parser.TryParse("abcdefgh", out var result));
        Assert.Equal('a', result.Item1);
        Assert.Equal('b', result.Item2);
        Assert.Equal('c', result.Item3);
        Assert.Equal('d', result.Item4);
        Assert.Equal('e', result.Item5);
        Assert.Equal('f', result.Item6);
        Assert.Equal('g', result.Item7);
    }

    [Fact]
    public void SequenceSkipAnd4ShouldParseFourElements()
    {
        var parser = Literals.Char('a')
            .SkipAnd(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'));

        Assert.True(parser.TryParse("abcd", out var result));
        Assert.Equal('b', result.Item1);
        Assert.Equal('c', result.Item2);
        Assert.Equal('d', result.Item3);
    }

    [Fact]
    public void SequenceSkipAnd5ShouldParseFiveElements()
    {
        var parser = Literals.Char('a')
            .SkipAnd(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'))
            .And(Literals.Char('e'));

        Assert.True(parser.TryParse("abcde", out var result));
        Assert.Equal('b', result.Item1);
        Assert.Equal('c', result.Item2);
        Assert.Equal('d', result.Item3);
        Assert.Equal('e', result.Item4);
    }

    [Fact]
    public void SequenceSkipAnd6ShouldParseSixElements()
    {
        var parser = Literals.Char('a')
            .SkipAnd(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'))
            .And(Literals.Char('e'))
            .And(Literals.Char('f'));

        Assert.True(parser.TryParse("abcdef", out var result));
        Assert.Equal('b', result.Item1);
        Assert.Equal('c', result.Item2);
        Assert.Equal('d', result.Item3);
        Assert.Equal('e', result.Item4);
        Assert.Equal('f', result.Item5);
    }

    [Fact]
    public void SequenceSkipAnd7ShouldParseSevenElements()
    {
        var parser = Literals.Char('a')
            .SkipAnd(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'))
            .And(Literals.Char('e'))
            .And(Literals.Char('f'))
            .And(Literals.Char('g'));

        Assert.True(parser.TryParse("abcdefg", out var result));
        Assert.Equal('b', result.Item1);
        Assert.Equal('c', result.Item2);
        Assert.Equal('d', result.Item3);
        Assert.Equal('e', result.Item4);
        Assert.Equal('f', result.Item5);
        Assert.Equal('g', result.Item6);
    }

    [Fact]
    public void SequenceSkipAnd8ShouldParseEightElements()
    {
        var parser = Literals.Char('a')
            .SkipAnd(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'))
            .And(Literals.Char('e'))
            .And(Literals.Char('f'))
            .And(Literals.Char('g'))
            .And(Literals.Char('h'));

        Assert.True(parser.TryParse("abcdefgh", out var result));
        Assert.Equal('b', result.Item1);
        Assert.Equal('c', result.Item2);
        Assert.Equal('d', result.Item3);
        Assert.Equal('e', result.Item4);
        Assert.Equal('f', result.Item5);
        Assert.Equal('g', result.Item6);
        Assert.Equal('h', result.Item7);
    }

    [Fact]
    public void SequenceAndSkipShouldFailOnMismatch()
    {
        var parser = Literals.Char('a')
            .And(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'))
            .AndSkip(Literals.Char('e'));

        Assert.False(parser.TryParse("abcdx", out _));
    }

    [Fact]
    public void SequenceSkipAndShouldFailOnMismatch()
    {
        var parser = Literals.Char('a')
            .SkipAnd(Literals.Char('b'))
            .And(Literals.Char('c'))
            .And(Literals.Char('d'));

        Assert.False(parser.TryParse("axcd", out _));
    }

    [Fact]
    public void SequenceAndSkipShouldWorkWithDifferentTypes()
    {
        var parser = Literals.Integer()
            .And(Literals.Char(','))
            .And(Literals.Integer())
            .And(Literals.Char(','))
            .And(Literals.Integer())
            .AndSkip(Literals.Char(';'));

        Assert.True(parser.TryParse("1,2,3;", out var result));
        Assert.Equal(1L, result.Item1);
        Assert.Equal(',', result.Item2);
        Assert.Equal(2L, result.Item3);
        Assert.Equal(',', result.Item4);
        Assert.Equal(3L, result.Item5);
    }

    [Fact]
    public void SequenceSkipAndShouldWorkWithDifferentTypes()
    {
        var parser = Literals.Char('(')
            .SkipAnd(Literals.Integer())
            .And(Literals.Char(','))
            .And(Literals.Integer())
            .And(Literals.Char(','))
            .And(Literals.Integer());

        Assert.True(parser.TryParse("(10,20,30", out var result));
        Assert.Equal(10L, result.Item1);
        Assert.Equal(',', result.Item2);
        Assert.Equal(20L, result.Item3);
        Assert.Equal(',', result.Item4);
        Assert.Equal(30L, result.Item5);
    }
}
