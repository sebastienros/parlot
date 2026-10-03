using System;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class HitEndTests
{
    [Fact]
    public void FinalCursorNeverReportsHitEnd()
    {
        var c = new Cursor("ab");

        Assert.True(c.IsFinal);
        c.Advance(5);
        _ = c.PeekNext(3);
        c.MarkHitEnd();

        Assert.True(c.Eof);
        Assert.False(c.HitEnd);
    }

    [Fact]
    public void EmptyNonFinalCursorHitsEnd()
    {
        var c = new Cursor("", TextPosition.Start, isFinal: false);

        Assert.True(c.Eof);
        Assert.True(c.HitEnd);
    }

    [Fact]
    public void AdvanceToEndHitsEnd()
    {
        var c = new Cursor("ab", TextPosition.Start, isFinal: false);

        c.Advance();
        Assert.False(c.HitEnd);

        c.Advance();
        Assert.True(c.HitEnd);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    public void AdvanceCountToEndHitsEnd(int count)
    {
        var text = new string('a', 100);
        var c = new Cursor(text, new TextPosition(100 - count, 1, 1), isFinal: false);

        c.Advance(count);

        Assert.True(c.HitEnd);
    }

    [Fact]
    public void AdvanceNoNewLinesToEndHitsEnd()
    {
        var c = new Cursor("abc", TextPosition.Start, isFinal: false);

        c.AdvanceNoNewLines(2);
        Assert.False(c.HitEnd);

        c.AdvanceNoNewLines(1);
        Assert.True(c.HitEnd);
    }

    [Fact]
    public void AdvanceByToEndHitsEnd()
    {
        var c = new Cursor("abc", TextPosition.Start, isFinal: false);

        c.AdvanceBy(2, 0, 2);
        Assert.False(c.HitEnd);

        c.AdvanceBy(1, 0, 1);
        Assert.True(c.HitEnd);
    }

    [Fact]
    public void PeekPastEndHitsEnd()
    {
        var c = new Cursor("abc", TextPosition.Start, isFinal: false);

        Assert.Equal('c', c.PeekNext(2));
        Assert.False(c.HitEnd);

        Assert.Equal(Cursor.NullChar, c.PeekNext(3));
        Assert.True(c.HitEnd);
    }

    [Fact]
    public void HitEndIsStickyAcrossResetPosition()
    {
        var c = new Cursor("abc", TextPosition.Start, isFinal: false);
        var start = c.Position;

        c.Advance(3);
        c.ResetPosition(start);

        Assert.Equal(0, c.Offset);
        Assert.True(c.HitEnd);
    }

    [Fact]
    public void ResetPositionToEndHitsEnd()
    {
        var c = new Cursor("abc", TextPosition.Start, isFinal: false);

        c.ResetPosition(new TextPosition(3, 1, 4));

        Assert.True(c.HitEnd);
    }

    [Theory]
    [InlineData("ab", "abc", true)]
    [InlineData("ab", "abc", true, StringComparison.Ordinal)]
    [InlineData("AB", "abc", true, StringComparison.OrdinalIgnoreCase)]
    [InlineData("ax", "abc", false)]
    [InlineData("ax", "abc", false, StringComparison.OrdinalIgnoreCase)]
    [InlineData("ax", "abc", true, StringComparison.InvariantCulture)]
    [InlineData("abc", "ab", false)]
    [InlineData("abd", "abc", false)]
    public void MatchOnlyHitsEndWhenTheRemainderIsAPrefix(string buffer, string pattern, bool hitEnd, StringComparison? comparison = null)
    {
        var c = new Cursor(buffer, TextPosition.Start, isFinal: false);

        var matched = comparison is null ? c.Match(pattern) : c.Match(pattern, comparison.Value);

        Assert.Equal(buffer.StartsWith(pattern, StringComparison.Ordinal), matched);
        Assert.Equal(hitEnd, c.HitEnd);
        Assert.Equal(0, c.Offset);
    }

    [Fact]
    public void MatchOnFinalBufferDoesNotHitEnd()
    {
        var c = new Cursor("ab", TextPosition.Start, isFinal: true);

        Assert.False(c.Match("abc"));
        Assert.False(c.HitEnd);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1.")]
    [InlineData("1.5")]
    [InlineData("1e")]
    [InlineData("1e+")]
    [InlineData("-")]
    public void ReadDecimalAtEndHitsEnd(string text)
    {
        var s = new Scanner(text, isFinal: false);

        s.ReadDecimal();

        Assert.True(s.Cursor.HitEnd);
    }

    [Fact]
    public void ReadDecimalBeforeEndDoesNotHitEnd()
    {
        var s = new Scanner("12.5 ", isFinal: false);

        Assert.True(s.ReadDecimal(out var number));
        Assert.Equal("12.5", number.ToString());
        Assert.False(s.Cursor.HitEnd);
    }

    [Theory]
    [InlineData("\"abc")]
    [InlineData("\"ab\\\"c")]
    [InlineData("\"ab\\")]
    [InlineData("\"ab\\u12")]
    [InlineData("\"ab\\x12")]
    [InlineData("\"ab\\n")]
    [InlineData("\"")]
    public void UnterminatedQuotedStringHitsEndAndRestoresCursor(string text)
    {
        var s = new Scanner(text, isFinal: false);

        Assert.False(s.ReadDoubleQuotedString());
        Assert.True(s.Cursor.HitEnd);
        Assert.Equal(0, s.Cursor.Offset);
        Assert.Equal(1, s.Cursor.Position.Column);
    }

    [Theory]
    [InlineData("\"abc")]
    [InlineData("\"ab\\\"c")]
    [InlineData("\"ab\\u12")]
    [InlineData("\"ab\\x12")]
    public void UnterminatedQuotedStringFailsOnFinalBuffer(string text)
    {
        var s = new Scanner(text);

        Assert.False(s.ReadDoubleQuotedString());
        Assert.False(s.Cursor.HitEnd);
        Assert.Equal(0, s.Cursor.Offset);
    }

    [Theory]
    [InlineData("\"abc\" ")]
    [InlineData("\"a\\nb\" ")]
    [InlineData("\"a\\u1234b\" ")]
    public void TerminatedQuotedStringDoesNotHitEnd(string text)
    {
        var s = new Scanner(text, isFinal: false);

        Assert.True(s.ReadDoubleQuotedString());
        Assert.False(s.Cursor.HitEnd);
    }

    [Fact]
    public void InvalidEscapeIsConclusive()
    {
        var s = new Scanner("\"a\\qb\" ", isFinal: false);

        Assert.False(s.ReadDoubleQuotedString());
        Assert.False(s.Cursor.HitEnd);
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("abc ", false)]
    public void ReadWhileHitsEndOnlyWhenReachingIt(string text, bool hitEnd)
    {
        var s = new Scanner(text, isFinal: false);

        Assert.True(s.ReadWhile(char.IsLetter));
        Assert.Equal(hitEnd, s.Cursor.HitEnd);
    }

    [Theory]
    [InlineData("   ", true)]
    [InlineData("  a", false)]
    public void SkipWhiteSpaceHitsEndOnlyWhenReachingIt(string text, bool hitEnd)
    {
        var s = new Scanner(text, isFinal: false);

        s.SkipWhiteSpaceOrNewLine();
        Assert.Equal(hitEnd, s.Cursor.HitEnd);
    }

    [Fact]
    public void ScannerStartsAtGivenPosition()
    {
        var s = new Scanner("abc", new TextPosition(1, 4, 9), isFinal: false);

        Assert.Equal('b', s.Cursor.Current);
        Assert.Equal(4, s.Cursor.Position.Line);
        Assert.Equal(9, s.Cursor.Position.Column);
        Assert.False(s.Cursor.IsFinal);
    }

    [Theory]
    [InlineData("ab", true)]
    [InlineData("a", true)]
    [InlineData("", true)]
    [InlineData("abx", false)]
    [InlineData("x", false)]
    public void AnyOfBelowMinSizeHitsEndOnlyWhenReachingIt(string text, bool hitEnd)
    {
        var parser = Literals.AnyOf("ab", minSize: 3);
        var context = new ParseContext(new Scanner(text, isFinal: false));
        var result = new ParseResult<TextSpan>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(hitEnd, context.Scanner.Cursor.HitEnd);
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("ab", true)]
    [InlineData("abx", false)]
    public void NoneOfBelowMinSizeHitsEndOnlyWhenReachingIt(string text, bool hitEnd)
    {
        var parser = Literals.NoneOf("x", minSize: 3);
        var context = new ParseContext(new Scanner(text, isFinal: false));
        var result = new ParseResult<TextSpan>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(hitEnd, context.Scanner.Cursor.HitEnd);
    }

    [Fact]
    public void AnyOfBelowMinSizeOnFinalBufferFails()
    {
        var context = new ParseContext(new Scanner("ab"));
        var result = new ParseResult<TextSpan>();

        Assert.False(Literals.AnyOf("ab", minSize: 3).Parse(context, ref result));
        Assert.False(context.Scanner.Cursor.HitEnd);
    }
}
