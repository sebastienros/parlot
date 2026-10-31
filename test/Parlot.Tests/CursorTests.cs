using Parlot.Fluent;
using System;
using Xunit;
using static Parlot.Fluent.Parsers;

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
    public void ShouldStartAtGivenPosition()
    {
        var c = new Cursor("ab\ncd", new TextPosition(3, 7, 1));

        Assert.Equal(3, c.Offset);
        Assert.Equal('c', c.Current);
        Assert.False(c.Eof);
        Assert.Equal(7, c.Position.Line);
        Assert.Equal(1, c.Position.Column);

        c.Advance();

        Assert.Equal(new TextPosition(4, 7, 2).ToString(), c.Position.ToString());
        Assert.Equal(4, c.Position.Offset);
        Assert.Equal('d', c.Current);
    }

    [Fact]
    public void ShouldStartAtEndPosition()
    {
        var c = new Cursor("ab", new TextPosition(2, 1, 3));

        Assert.True(c.Eof);
        Assert.Equal(Cursor.NullChar, c.Current);
        Assert.Equal(2, c.Offset);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cursor("ab", new TextPosition(3, 1, 1)));
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
        Assert.Equal(4, c.Position.Column);
        Assert.Equal(2, c.Position.Line);
        c.Advance();

        Assert.Equal('\n', c.Current);
        Assert.Equal(8, c.Position.Offset);
        Assert.Equal(5, c.Position.Column);
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
        Assert.Equal(5, c.Position.Column);
        Assert.Equal(2, c.Position.Line);
    }

    [Theory]
    [InlineData("hello", 0, 5)]
    [InlineData("hello", 0, 8)]
    [InlineData("hello", 2, 3)]
    [InlineData("hello", 4, 1)]
    [InlineData("hello", 5, 1)]
    [InlineData("h", 0, 1)]
    [InlineData("", 0, 1)]
    public void AdvanceNoNewLinesToEofShouldCountColumnsLikeAdvance(string text, int start, int count)
    {
        var expected = new Cursor(text);
        expected.Advance(start);
        expected.Advance(count);

        var c = new Cursor(text);
        c.Advance(start);
        c.AdvanceNoNewLines(count);

        Assert.True(c.Eof);
        Assert.Equal(Cursor.NullChar, c.Current);
        Assert.Equal(text.Length, c.Position.Offset);
        Assert.Equal(expected.Position.Column, c.Position.Column);
        Assert.Equal(text.Length + 1, c.Position.Column);
        Assert.Equal(1, c.Position.Line);
    }

    [Fact]
    public void ErrorAtEofShouldReportTheColumnAfterTheText()
    {
        var parser = Literals.Text("hello").And(Literals.Text("x").ElseError("expected x"));

        Assert.False(parser.TryParse("hello", out _, out var error));
        Assert.NotNull(error);
        Assert.Equal(5, error.Position.Offset);
        Assert.Equal(1, error.Position.Line);
        Assert.Equal(6, error.Position.Column);
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

        Assert.Equal("", t.T.ToString());
        Assert.Equal(0, t.T.Length);
        Assert.Equal(0, t.T.Offset);

        var t2 = new TextSpan(null);

        Assert.Equal("", t2.ToString());
        Assert.Equal(0, t2.Length);
        Assert.Equal(0, t2.Offset);

#if NET8_0_OR_GREATER
        Assert.True(ReadOnlySpan<char>.Empty == t2.Span);
#endif
    }

    // Only '\n' ends a line; every other char, '\r' included, occupies one column. All the ways of moving the cursor agree.
    public static TheoryData<string> LineTrackingInputs => new()
    {
        "abc",
        "ab\n",
        "ab\r",
        "ab\rc",
        "ab\r\n",
        "ab\r\ncd",
        "\n",
        "\r\n",
        "\r",
        "\n\n\n",
        "a\n\nb c",
        new string('a', 70) + "\rb",
        new string('a', 70) + "\n",
        new string('a', 70) + "\r\n" + new string('b', 70),
        new string('a', 40) + "\r" + new string('b', 40) + "\n",
    };

    private static TextPosition ExpectedPosition(string text, int offset)
    {
        var line = 1;
        var column = 1;

        for (var i = 0; i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new TextPosition(offset, line, column);
    }

    private static void AssertPosition(TextPosition expected, Cursor cursor)
    {
        Assert.Equal(expected.Offset, cursor.Position.Offset);
        Assert.Equal(expected.Line, cursor.Position.Line);
        Assert.Equal(expected.Column, cursor.Position.Column);
        Assert.Equal(expected.Offset == cursor.Buffer.Length, cursor.Eof);
        Assert.Equal(expected.Offset == cursor.Buffer.Length ? Cursor.NullChar : cursor.Buffer[expected.Offset], cursor.Current);
    }

    [Theory]
    [MemberData(nameof(LineTrackingInputs))]
    public void AdvanceShouldCountEveryCharButLineFeedsAsAColumn(string text)
    {
        var cursor = new Cursor(text);

        for (var offset = 1; offset <= text.Length; offset++)
        {
            cursor.Advance();
            AssertPosition(ExpectedPosition(text, offset), cursor);
        }
    }

    [Theory]
    [MemberData(nameof(LineTrackingInputs))]
    public void AdvanceByCountShouldAgreeWithAdvance(string text)
    {
        for (var start = 0; start <= text.Length; start++)
        {
            for (var count = 0; count <= text.Length - start + 1; count++)
            {
                var cursor = new Cursor(text);
                cursor.Advance(start);
                cursor.Advance(count);

                AssertPosition(ExpectedPosition(text, Math.Min(start + count, text.Length)), cursor);
            }
        }
    }

    [Theory]
    [MemberData(nameof(LineTrackingInputs))]
    public void AdvanceByShouldAgreeWithAdvance(string text)
    {
        for (var start = 0; start <= text.Length; start++)
        {
            for (var count = 0; count <= text.Length - start; count++)
            {
                var cursor = new Cursor(text);
                cursor.Advance(start);

                var consumed = text.AsSpan(start, count);
                var newLines = 0;
                var trailing = 0;

                foreach (var c in consumed)
                {
                    if (c == '\n')
                    {
                        newLines++;
                        trailing = 0;
                    }
                    else
                    {
                        trailing++;
                    }
                }

                cursor.AdvanceBy(count, newLines, trailing);

                AssertPosition(ExpectedPosition(text, start + count), cursor);
            }
        }
    }

    [Theory]
    [MemberData(nameof(LineTrackingInputs))]
    public void AdvanceNoNewLinesShouldAgreeWithAdvance(string text)
    {
        for (var start = 0; start <= text.Length; start++)
        {
            for (var count = 0; count <= text.Length - start + 1; count++)
            {
                if (text.AsSpan(start, Math.Min(count, text.Length - start)).IndexOf('\n') >= 0)
                {
                    // Not a valid use
                    continue;
                }

                var cursor = new Cursor(text);
                cursor.Advance(start);
                cursor.AdvanceNoNewLines(count);

                AssertPosition(ExpectedPosition(text, Math.Min(start + count, text.Length)), cursor);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a\n")]
    public void AdvanceShouldStayAtEof(string text)
    {
        var cursor = new Cursor(text);
        cursor.Advance(text.Length);

        var end = cursor.Position;
        Assert.True(cursor.Eof);

        cursor.Advance();
        cursor.Advance(1);
        cursor.AdvanceNoNewLines(1);

        AssertPosition(end, cursor);
        Assert.True(cursor.Span.IsEmpty);
    }

    // The line and column are computed when the position is read, from the last position read or the initial one. They
    // must match a char by char count from the initial position whatever the cursor did in between.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void LazyPositionsMatchANaiveCount(int seed)
    {
        var random = new Random(seed);
        const string alphabet = "ab \n\r\n\n";

        for (var iteration = 0; iteration < 200; iteration++)
        {
            // Longer texts reach the vectorized counts
            var length = random.Next(4) == 0 ? random.Next(64, 400) : random.Next(0, 24);
            var chars = new char[length];

            for (var i = 0; i < length; i++)
            {
                chars[i] = alphabet[random.Next(alphabet.Length)];
            }

            var text = new string(chars);
            var start = random.Next(3) == 0
                ? new TextPosition(random.Next(text.Length + 1), random.Next(1, 10), random.Next(1, 10))
                : TextPosition.Start;

            var cursor = new Cursor(text, start);
            var offset = start.Offset;

            for (var step = 0; step < 60; step++)
            {
                var count = random.Next(10) == 0 ? random.Next(0, text.Length + 2) : random.Next(0, 6);

                switch (random.Next(8))
                {
                    case 0:
                        cursor.Advance();
                        offset = Math.Min(offset + 1, text.Length);
                        break;
                    case 1:
                        cursor.Advance(count);
                        offset = Math.Min(offset + count, text.Length);
                        break;
                    case 2:
                        // Negative counts are ignored
                        cursor.Advance(-count - 1);
                        break;
                    case 3:
                        cursor.AdvanceNoNewLines(count);
                        offset = Math.Min(offset + count, text.Length);
                        break;
                    case 4:
                        // The line arguments are ignored
                        cursor.AdvanceBy(Math.Min(count, text.Length - offset), random.Next(3), random.Next(3));
                        offset = Math.Min(offset + count, text.Length);
                        break;
                    case 5:
                        offset = random.Next(start.Offset, text.Length + 1);
                        cursor.ResetPosition(offset);
                        break;
                    case 6:
                        // Only the offset of a position is used
                        offset = random.Next(start.Offset, text.Length + 1);
                        cursor.ResetPosition(new TextPosition(offset, random.Next(100), random.Next(100)));
                        break;
                    default:
                        // Moves several times before reading the position again
                        continue;
                }

                Assert.Equal(offset, cursor.Offset);
                Assert.Equal(offset == text.Length, cursor.Eof);
                Assert.Equal(offset == text.Length ? Cursor.NullChar : text[offset], cursor.Current);

                if (random.Next(3) != 0)
                {
                    var expected = ExpectedPosition(text, start, offset);
                    var actual = cursor.Position;

                    Assert.True(expected.Offset == actual.Offset && expected.Line == actual.Line && expected.Column == actual.Column,
                        $"'{Escape(text)}' from {start.Offset}{start} at {offset}: expected {expected}, actual {actual}");
                }
            }
        }
    }

    private static TextPosition ExpectedPosition(string text, TextPosition start, int offset)
    {
        var line = start.Line;
        var column = start.Column;

        for (var i = start.Offset; i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new TextPosition(offset, line, column);
    }

    private static string Escape(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n");

    [Fact]
    public void ResetPositionOnlyUsesTheOffset()
    {
        var cursor = new Cursor("ab\r\ncd\nef");

        cursor.Advance(8);
        cursor.ResetPosition(new TextPosition(5, 42, 42));

        Assert.Equal(new TextPosition(5, 2, 2), cursor.Position);
        Assert.Equal('d', cursor.Current);

        cursor.Advance(4);
        Assert.Equal(new TextPosition(9, 3, 3), cursor.Position);
    }

    [Fact]
    public void ResetPositionToAnOffsetDoesntChangeTheLinesOfTheInitialPosition()
    {
        var cursor = new Cursor("ab\ncd\nef", new TextPosition(3, 7, 4));

        cursor.Advance(4);
        Assert.Equal(new TextPosition(7, 8, 2), cursor.Position);

        cursor.ResetPosition(3);
        Assert.Equal(new TextPosition(3, 7, 4), cursor.Position);

        cursor.ResetPosition(5);
        Assert.Equal(new TextPosition(5, 7, 6), cursor.Position);
    }

    [Theory]
    [InlineData(2, 6, 3)] // The '\n' before the initial position, the text before the buffer is assumed to start a line
    [InlineData(1, 6, 2)]
    [InlineData(0, 6, 1)]
    public void PositionsBeforeTheInitialOneAreCountedBackward(int offset, int line, int column)
    {
        var cursor = new Cursor("ab\ncd", new TextPosition(3, 7, 1));

        cursor.ResetPosition(offset);

        Assert.Equal(new TextPosition(offset, line, column), cursor.Position);

        // The initial position isn't changed by it
        cursor.ResetPosition(5);
        Assert.Equal(new TextPosition(5, 7, 3), cursor.Position);
    }

    [Fact]
    public void PositionsBeforeTheInitialOneOnItsLineAreCountedBackward()
    {
        var cursor = new Cursor("abcd", new TextPosition(3, 2, 5));

        cursor.ResetPosition(1);
        Assert.Equal(new TextPosition(1, 2, 3), cursor.Position);

        // Never before the first column
        cursor = new Cursor("abcd", new TextPosition(3, 2, 1));
        cursor.ResetPosition(1);
        Assert.Equal(new TextPosition(1, 2, 1), cursor.Position);
    }

    [Fact]
    public void PositionsAreReadInAnyOrder()
    {
        var text = new string('a', 100) + "\n" + new string('b', 100) + "\r\n" + new string('c', 100);
        var cursor = new Cursor(text);

        foreach (var offset in new[] { 300, 50, 250, 101, 102, 100, 0, 302, 205, 203, 204, text.Length, 201, 202, 1 })
        {
            cursor.ResetPosition(offset);
            Assert.Equal(ExpectedPosition(text, TextPosition.Start, offset), cursor.Position);
        }
    }
#pragma warning restore
}
