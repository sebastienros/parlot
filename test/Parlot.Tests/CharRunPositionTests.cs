using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

// Pattern, NonWhiteSpace, Identifier and Scanner.ReadWhile count the chars they read before moving the cursor once.
// The position they end at must be the one obtained by advancing the cursor one char at a time.
public class CharRunPositionTests
{
    public static TheoryData<string, int> Inputs => new()
    {
        { "abc", 3 },
        { "abc def", 3 },
        { "abc\ndef", 7 },
        { "abc\r\ndef", 8 },
        { "\nabc", 4 },
        { "\rabc ", 4 },
        { "ab\n", 3 },
        { "ab\r", 3 },
        { "ab\r\n", 4 },
        { "ab\rc", 4 },
        { "a\n\nb c", 4 },
        { "a", 1 },
    };

    private static TextPosition Expected(string text, int length)
    {
        var cursor = new Cursor(text);

        for (var i = 0; i < length; i++)
        {
            cursor.Advance();
        }

        return cursor.Position;
    }

    private static void AssertPosition(TextPosition expected, Cursor cursor)
    {
        Assert.Equal(expected.Offset, cursor.Position.Offset);
        Assert.Equal(expected.Line, cursor.Position.Line);
        Assert.Equal(expected.Column, cursor.Position.Column);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void PatternShouldTrackThePosition(string text, int length)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<TextSpan>();

        Assert.True(Literals.Pattern(static c => c != ' ').Parse(context, ref result));
        Assert.Equal(text.Substring(0, length), result.Value.ToString());
        AssertPosition(Expected(text, length), context.Scanner.Cursor);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void PatternShouldTrackThePositionWithMaxSize(string text, int length)
    {
        var size = length - 1;

        if (size == 0)
        {
            return;
        }

        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<TextSpan>();

        Assert.True(Literals.Pattern(static c => c != ' ', maxSize: size).Parse(context, ref result));
        Assert.Equal(text.Substring(0, size), result.Value.ToString());
        AssertPosition(Expected(text, size), context.Scanner.Cursor);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void ReadWhileShouldTrackThePosition(string text, int length)
    {
        var scanner = new Scanner(text);

        Assert.True(scanner.ReadWhile(static c => c != ' ', out var read));
        Assert.Equal(text.Substring(0, length), read.ToString());
        AssertPosition(Expected(text, length), scanner.Cursor);
    }

    [Fact]
    public void PatternShouldNotMoveWhenTooShort()
    {
        var context = new ParseContext(new Scanner("ab\ncd e"));
        var result = new ParseResult<TextSpan>();

        Assert.False(Literals.Pattern(static c => c != ' ', minSize: 6).Parse(context, ref result));
        AssertPosition(TextPosition.Start, context.Scanner.Cursor);
    }

    [Theory]
    [InlineData("abc", 3)]
    [InlineData("abc def", 3)]
    [InlineData("a", 1)]
    [InlineData("a-b@c d", 3)]
    public void IdentifierShouldTrackThePosition(string text, int length)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<TextSpan>();

        Assert.True(new Identifier(static c => c == '@', static c => c == '-').Parse(context, ref result));
        Assert.Equal(text.Substring(0, length), result.Value.ToString());
        AssertPosition(Expected(text, length), context.Scanner.Cursor);
    }
    [Theory]
    [InlineData("abc\n")]
    [InlineData("abc\r\n")]
    public void RunsStoppingBeforeNewLineTrackPosition(string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<TextSpan>();
        Assert.True(Literals.Pattern(static c => Character.IsIdentifierPart(c)).Parse(context, ref result));
        AssertPosition(Expected(text, 3), context.Scanner.Cursor);
        Assert.Equal("abc", result.Value.ToString());

        var scanner = new Scanner(text);
        Assert.True(scanner.ReadWhile(static c => Character.IsIdentifierPart(c), out var value));
        AssertPosition(Expected(text, 3), scanner.Cursor);
        Assert.Equal("abc", value.ToString());
    }

}
