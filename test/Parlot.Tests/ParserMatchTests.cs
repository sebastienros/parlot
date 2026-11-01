using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class ParserMatchTests
{
    private static readonly Parser<(TextSpan, char, long)> Assignment = Terms.Identifier().And(Terms.Char('=')).And(Terms.Integer());

    [Theory]
    [InlineData("age = 12", true)]
    [InlineData("age = 12; next", true)]
    [InlineData("  age=12", true)]
    [InlineData("age =", false)]
    [InlineData("= 12", false)]
    [InlineData("", false)]
    public void IsMatchShouldMatchTheStartOfTheText(string text, bool expected)
    {
        Assert.Equal(expected, Assignment.IsMatch(text));
        Assert.Equal(expected, Assignment.IsMatch(text, CancellationToken.None));
        Assert.Equal(expected, Assignment.IsMatch(new ParseContext(new Scanner(text))));
        Assert.Equal(expected, Assignment.TryParse(text, out _));
    }

    [Fact]
    public void IsMatchShouldMatchTheWholeTextWithEof()
    {
        Assert.True(Assignment.Eof().IsMatch("age = 12"));
        Assert.False(Assignment.Eof().IsMatch("age = 12; next"));
    }

    [Theory]
    [InlineData("age = 12", "age = 12")]
    [InlineData("age = 12; next", "age = 12")]
    [InlineData("  age=12  ", "  age=12")]
    public void TryMatchShouldReturnTheMatchedText(string text, string expected)
    {
        Assert.True(Assignment.TryMatch(text, out var match));
        Assert.Equal(expected, match.ToString());
        Assert.Equal(0, match.Offset);
        Assert.Same(text, match.Buffer);

        // The text Capture returns
        Assert.Equal(Capture(Assignment).Parse(text).ToString(), match.ToString());

        Assert.True(Assignment.TryMatch(text, CancellationToken.None, out match));
        Assert.Equal(expected, match.ToString());
        Assert.True(Assignment.TryMatch(text, out match, out var error));
        Assert.Equal(expected, match.ToString());
        Assert.Null(error);
        Assert.True(Assignment.TryMatch(text, CancellationToken.None, out match, out error));
        Assert.Equal(expected, match.ToString());
        Assert.Null(error);
    }

    [Theory]
    [InlineData("age =")]
    [InlineData("= 12")]
    [InlineData("")]
    public void TryMatchShouldFailWithoutText(string text)
    {
        Assert.False(Assignment.TryMatch(text, out var match, out var error));
        Assert.Equal(default, match);
        Assert.Null(error);
    }

    [Fact]
    public void TryMatchShouldMatchAtTheCursorOfTheContext()
    {
        var context = new ParseContext(new Scanner("x = 1; y = 2; z"));

        Assert.True(Assignment.TryMatch(context, out var match, out _));
        Assert.Equal("x = 1", match.ToString());
        Assert.True(Terms.Char(';').IsMatch(context));

        Assert.True(Assignment.TryMatch(context, out match, out _));
        Assert.Equal(" y = 2", match.ToString());
        Assert.Equal(6, match.Offset);
        Assert.Equal(12, context.Scanner.Cursor.Offset);

        // The cursor is back where it was when it fails
        Assert.False(Assignment.TryMatch(context, out _, out _));
        Assert.Equal(12, context.Scanner.Cursor.Offset);
        Assert.False(Terms.Char(';').And(Assignment).IsMatch(context));
        Assert.Equal(12, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void TryMatchShouldMatchAnEmptyText()
    {
        Assert.True(ZeroOrMany(Terms.Char('a')).TryMatch("bc", out var match));
        Assert.Equal("", match.ToString());
        Assert.Equal(0, match.Length);
    }

    [Fact]
    public void TryMatchShouldReturnErrors()
    {
        var parser = Terms.Char('a').And(Terms.Char('b').ElseError("Expected b"));

        Assert.False(parser.IsMatch("a c"));
        Assert.False(parser.TryMatch("a c", out var match, out var error));
        Assert.Equal(default, match);
        Assert.NotNull(error);
        Assert.Equal("Expected b", error.Message);

        // The error TryParse reports
        Assert.False(parser.TryParse("a c", out _, out var parseError));
        Assert.Equal(parseError!.Position, error.Position);
    }

    [Fact]
    public void TryMatchShouldFailWhenCancelled()
    {
        var parser = Separated(Terms.Char(','), Terms.Integer());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.False(parser.IsMatch("1,2,3,4,5", cts.Token));
        Assert.False(parser.TryMatch("1,2,3,4,5", cts.Token, out _, out var error));
        Assert.NotNull(error);
        Assert.Contains("canceled", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMatchShouldInvokeCallbacks()
    {
        var count = 0;
        var parser = OneOrMany(Terms.Integer().Then(x => { count++; return x; })).When((_, list) => list.Count > 1);

        Assert.True(parser.TryMatch("1 2 3 x", out var match));
        Assert.Equal("1 2 3", match.ToString());
        Assert.Equal(3, count);

        // The predicate reads the list
        Assert.False(parser.IsMatch("1 x"));
    }

    [Fact]
    public void TryMatchShouldThrowWithoutContext()
    {
        Assert.Throws<ArgumentNullException>(() => Assignment.TryMatch((ParseContext)null!, out _, out _));
    }

    [Theory]
    [InlineData("a.b.c")]
    [InlineData("a")]
    [InlineData(".a")]
    [InlineData("a..b")]
    [InlineData("")]
    public void TryMatchShouldMatchWhatTryParseParses(string text)
    {
        AssertSameAsTryParse(Separated(Literals.Char('.'), Literals.Identifier()), text);
        AssertSameAsTryParse(Literals.Identifier().And(ZeroOrMany(Literals.Char('.').SkipAnd(Literals.Identifier()))), text);
        AssertSameAsTryParse(OneOf(OneOrMany(Literals.Pattern(static c => c == '.')), ZeroOrOne(Literals.Identifier())), text);
        AssertSameAsTryParse(Between(Literals.Identifier(), ZeroOrMany(Literals.Char('.')), Literals.Identifier()), text);
        AssertSameAsTryParse(Literals.Identifier().AndSkip(OneOrMany(Literals.Char('.'))).Optional(), text);

        static void AssertSameAsTryParse<T>(Parser<T> parser, string text)
        {
            var success = parser.TryMatch(text, out var match);

            Assert.Equal(parser.TryParse(text, out _), success);

            if (success)
            {
                Assert.Equal(Capture(parser).Parse(text).ToString(), match.ToString());
            }
        }
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData("a.b.c.d.e.f.g")]
    [InlineData("a")]
    public void TryMatchShouldNotAllocate(string text)
    {
        var parser = Literals.Identifier().And(ZeroOrMany(Literals.Char('.').SkipAnd(Literals.Identifier())));
        var context = new ParseContext(new Scanner(text));

        // Warm up, such that the tiering and the lazy initializations are not measured
        for (var i = 0; i < 100; i++)
        {
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            Assert.True(parser.TryMatch(context, out _, out _));
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            Assert.True(parser.IsMatch(context));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var match = default(TextSpan);

        for (var i = 0; i < 100; i++)
        {
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            parser.TryMatch(context, out match, out _);
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            parser.IsMatch(context);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(text, match.ToString());
    }
#endif
}
