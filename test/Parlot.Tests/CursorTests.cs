using System;
using Xunit;

namespace Parlot.Tests;

public class CursorTests
{
    [Fact]
    public void ShouldMatchString()
    {
        var c = new Cursor("Lorem ipsum", TextPosition.Start);

        Assert.True(c.Match(""));
        Assert.True(c.Match("Lorem"));
        Assert.True(c.Match("Lorem ipsum"));
    }

    [Fact]
    public void ShouldMatchEmptyString()
    {
        var c = new Cursor("Lorem ipsum", TextPosition.Start);

        Assert.True(c.Match(""));
    }

    [Fact]
    public void ShouldNotMatchString()
    {
        var c = new Cursor("Lorem ipsum", TextPosition.Start);

        Assert.False(c.Match("Lorem ipsum dolor"));
    }

    [Fact]
    public void AdvanceShouldReturnOnEof()
    {
        var c = new Cursor("Lorem ipsum");

        for (var i = 0; i < c.Buffer.Length - 1; i++)
        {
            c.Advance();
            Assert.False(c.Eof);
        }

        Assert.Equal('m', c.Current);

        c.Advance();
        Assert.True(c.Eof);
        Assert.Equal(Cursor.NullChar, c.Current);

        c.Advance();
        Assert.True(c.Eof);
        Assert.Equal(Cursor.NullChar, c.Current);
    }

    [Fact]
    public void PeekShouldReturnFirstChar()
    {
        var c = new Cursor("123");

        Assert.Equal('1', c.Current);
        Assert.Equal('1', c.Current);
    }

    [Fact]
    public void AdvanceShouldCountLinesAndColumns()
    {
        var c = new Cursor("123\n456\r\n789");

        Assert.Equal('1', c.Current);
        Assert.Equal(0, c.Position.Offset);
        Assert.Equal(1, c.Position.Column);
        Assert.Equal(1, c.Position.Line);

        c.Advance();

        Assert.Equal('2', c.Current);
        Assert.Equal(1, c.Position.Offset);
        Assert.Equal(2, c.Position.Column);
        Assert.Equal(1, c.Position.Line);

        c.Advance();

        Assert.Equal('3', c.Current);
        Assert.Equal(2, c.Position.Offset);
        Assert.Equal(3, c.Position.Column);
        Assert.Equal(1, c.Position.Line);
        c.Advance();

        Assert.Equal('\n', c.Current);
        Assert.Equal(3, c.Position.Offset);
        Assert.Equal(4, c.Position.Column);
        Assert.Equal(1, c.Position.Line);
        c.Advance();

        Assert.Equal('4', c.Current);
        Assert.Equal(4, c.Position.Offset);
        Assert.Equal(1, c.Position.Column);
        Assert.Equal(2, c.Position.Line);
        c.Advance();

        Assert.Equal('5', c.Current);
        Assert.Equal(5, c.Position.Offset);
        Assert.Equal(2, c.Position.Column);
        Assert.Equal(2, c.Position.Line);
        c.Advance();

        Assert.Equal('6', c.Current);
        Assert.Equal(6, c.Position.Offset);
        Assert.Equal(3, c.Position.Column);
        Assert.Equal(2, c.Position.Line);
        c.Advance();

        Assert.Equal('\r', c.Current);
        Assert.Equal(7, c.Position.Offset);
        Assert.Equal(3, c.Position.Column);
        Assert.Equal(2, c.Position.Line);
        c.Advance();

        Assert.Equal('\n', c.Current);
        Assert.Equal(8, c.Position.Offset);
        Assert.Equal(4, c.Position.Column);
        Assert.Equal(2, c.Position.Line);
        c.Advance();

        Assert.Equal('7', c.Current);
        Assert.Equal(9, c.Position.Offset);
        Assert.Equal(1, c.Position.Column);
        Assert.Equal(3, c.Position.Line);
        c.Advance();

        Assert.Equal('8', c.Current);
        Assert.Equal(10, c.Position.Offset);
        Assert.Equal(2, c.Position.Column);
        Assert.Equal(3, c.Position.Line);
        c.Advance();

        Assert.Equal('9', c.Current);
        Assert.Equal(11, c.Position.Offset);
        Assert.Equal(3, c.Position.Column);
        Assert.Equal(3, c.Position.Line);
        c.Advance();

        Assert.Equal(Cursor.NullChar, c.Current);
        Assert.Equal(12, c.Position.Offset);
        Assert.Equal(4, c.Position.Column);
        Assert.Equal(3, c.Position.Line);
    }

    [Fact]
    public void AdvanceShouldStopAtEof()
    {
        var c = new Cursor("1234");

        Assert.Equal('1', c.Current);
        Assert.Equal(0, c.Position.Offset);
        Assert.Equal(1, c.Position.Column);
        Assert.Equal(1, c.Position.Line);

        c.Advance(4);

        Assert.Equal(Cursor.NullChar, c.Current);
        Assert.Equal(4, c.Position.Offset);
        Assert.Equal(5, c.Position.Column);
        Assert.Equal(1, c.Position.Line);
    }

    [Fact]
    public void AdvanceNoNewLinesShouldCountColumns()
    {
        var c = new Cursor("123456789");

        Assert.Equal('1', c.Current);
        Assert.Equal(0, c.Position.Offset);
        Assert.Equal(1, c.Position.Column);
        Assert.Equal(1, c.Position.Line);

        c.AdvanceNoNewLines(4);

        Assert.Equal('5', c.Current);
        Assert.Equal(4, c.Position.Offset);
        Assert.Equal(5, c.Position.Column);
        Assert.Equal(1, c.Position.Line);

        c.AdvanceNoNewLines(4);

        Assert.Equal('9', c.Current);
        Assert.Equal(8, c.Position.Offset);
        Assert.Equal(9, c.Position.Column);
        Assert.Equal(1, c.Position.Line);

        c.AdvanceNoNewLines(1);

        Assert.Equal(Cursor.NullChar, c.Current);
        Assert.Equal(9, c.Position.Offset);
        Assert.Equal(10, c.Position.Column);
        Assert.Equal(1, c.Position.Line);
    }

    [Fact]
    public void AdvanceNoNewLinesShouldStopAtEof()
    {
        var c = new Cursor("1234\n5678");

        Assert.Equal('1', c.Current);
        Assert.Equal(0, c.Position.Offset);
        Assert.Equal(1, c.Position.Column);
        Assert.Equal(1, c.Position.Line);

        c.Advance(5);

        Assert.Equal('5', c.Current);
        Assert.Equal(5, c.Position.Offset);
        Assert.Equal(1, c.Position.Column);
        Assert.Equal(2, c.Position.Line);

        c.AdvanceNoNewLines(6);

        Assert.Equal(Cursor.NullChar, c.Current);
        Assert.Equal(9, c.Position.Offset);
        Assert.Equal(4, c.Position.Column);
        Assert.Equal(2, c.Position.Line);
    }

    [Fact]
    public void ResetPositionShouldMoveToEof()
    {
        var c = new Cursor("123");

        c.ResetPosition(new TextPosition(4, 1, 1));

        Assert.True(c.Eof);
        Assert.Equal(Cursor.NullChar, c.Current);
    }

    [Fact]
    public void PeekNextShouldReturnNullChar()
    {
        var c = new Cursor("123");

        Assert.Equal('1', c.PeekNext(0));
        Assert.Equal('2', c.PeekNext());
        Assert.Equal('2', c.PeekNext(1));
        Assert.Equal('3', c.PeekNext(2));
        Assert.Equal(Cursor.NullChar, c.PeekNext(3));
        Assert.Equal(Cursor.NullChar, c.PeekNext(4));
    }

    [Fact]
    public void MatchAnyOfShouldMatchAny()
    {
        var c = new Cursor("1234");

        Assert.True(c.MatchAnyOf(""));
        Assert.True(c.MatchAnyOf("1"));
        Assert.True(c.MatchAnyOf("abc1"));
        Assert.True(c.MatchAnyOf("123"));
        Assert.False(c.MatchAnyOf("abc"));

        c.ResetPosition(new TextPosition(4, 0, 0));

        Assert.False(c.MatchAnyOf(""));
        Assert.False(c.MatchAnyOf("1"));
        Assert.False(c.MatchAnyOf("abc1"));
        Assert.False(c.MatchAnyOf("123"));
        Assert.False(c.MatchAnyOf("abc"));
    }

    [Fact]
    public void MatchShouldMatch()
    {
        var c = new Cursor("1234");

        Assert.True(c.Match("1"));
        Assert.False(c.Match("2"));
        Assert.True(c.Match("123"));
        Assert.False(c.Match("11"));
        Assert.False(c.Match("122"));
        Assert.False(c.Match("1231"));

        c.ResetPosition(new TextPosition(4, 0, 0));

        Assert.False(c.Match("1"));
        Assert.False(c.Match("2"));
    }

#pragma warning disable CS0649
    private class TextSpanHolder
    {
        public TextSpan T;
    }

    [Fact]
    public void TextSpanShoudNotThrow()
    {
        var t = new TextSpanHolder();

        Assert.Null(t.T.ToString());
        Assert.Equal(0, t.T.Length);
        Assert.Equal(0, t.T.Offset);

        var t2 = new TextSpan(null);

        Assert.Null(t2.ToString());
        Assert.Equal(0, t2.Length);
        Assert.Equal(0, t2.Offset);

#if NET6_0_OR_GREATER
        Assert.True(ReadOnlySpan<char>.Empty == t2.Span);
#endif
    }
#pragma warning restore
}
