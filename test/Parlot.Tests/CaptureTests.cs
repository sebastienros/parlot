using Parlot.Fluent;
using System.Collections.Generic;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class CaptureTests
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void ShouldCaptureZeroOrManyUpToMaximum(int max, int count)
    {
        var parser = Capture(ZeroOrMany(Literals.Char('x'), max));
        var context = new ParseContext(new Scanner("xxx!"));
        var result = new ParseResult<TextSpan>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(new string('x', count), result.Value.ToString());
        Assert.Equal(0, result.Start);
        Assert.Equal(count, result.End);
        Assert.Equal(count, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void ShouldCaptureOneOrManyUpToMaximum(int max, int count)
    {
        var parser = Capture(OneOrMany(Literals.Char('x'), max));
        var context = new ParseContext(new Scanner("xxx!"));
        var result = new ParseResult<TextSpan>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(new string('x', count), result.Value.ToString());
        Assert.Equal(count, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldCaptureEmptyZeroOrMany()
    {
        var parser = Capture(ZeroOrMany(Literals.Char('x')));
        var context = new ParseContext(new Scanner("!"));
        var result = new ParseResult<TextSpan>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("", result.Value.ToString());
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("!")]
    [InlineData("a!")]
    public void CaptureOfOneOrManyShouldRestoreCursorOnFailure(string text)
    {
        var parser = Capture(OneOrMany(Literals.Text("ab")));
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<TextSpan>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void CaptureOfLoopShouldStopAtElementNotMovingCursor()
    {
        var element = Literals.Char('x').Optional();

        Assert.Equal("xx", Capture(ZeroOrMany(element)).Parse("xx!").ToString());
        Assert.Equal("xx", Capture(OneOrMany(element)).Parse("xx!").ToString());
        Assert.False(Capture(OneOrMany(element)).TryParse("!", out _));
    }

    [Fact]
    public void CaptureOfLoopShouldStopBeforeFailedElement()
    {
        // The element reads "a" before it fails on "ac", which must not be captured
        var parser = Capture(ZeroOrMany(Literals.Text("ab"))).And(Literals.Text("ac"));

        Assert.True(parser.TryParse("ababac", out var result));
        Assert.Equal("abab", result.Item1.ToString());
        Assert.Equal("ac", result.Item2);
    }

    [Fact]
    public void CaptureOfLoopShouldEnterTheLoop()
    {
        var loop = ZeroOrMany(Literals.Char('x'));
        var parser = Capture(loop);
        var entered = new List<object>();
        var exited = new List<object>();
        var context = new ParseContext(new Scanner("xx"))
        {
            OnEnterParser = (p, _) => entered.Add(p),
            OnExitParser = (p, _) => exited.Add(p),
        };
        var result = new ParseResult<TextSpan>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Contains(loop, entered);
        Assert.Contains(loop, exited);
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData("x")]
    [InlineData("xxxxxxxxxx")]
    public void CaptureOfLoopShouldNotAllocate(string text)
    {
        // Longer lists than the inline items of ListBuilder move them to the scratch stack
        Assert.Equal(0, MeasureAllocations(Capture(ZeroOrMany(Literals.Char('x'))), text));
        Assert.Equal(0, MeasureAllocations(Capture(OneOrMany(Literals.Char('x'))), text));
    }

    internal static long MeasureAllocations<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();

        // Warm up, such that the tiering and the lazy initializations are not measured
        for (var i = 0; i < 100; i++)
        {
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            Assert.True(parser.Parse(context, ref result));
        }

        var before = System.GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 100; i++)
        {
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            parser.Parse(context, ref result);
        }

        var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(text.Length, context.Scanner.Cursor.Offset);

        return allocated;
    }
#endif
}
