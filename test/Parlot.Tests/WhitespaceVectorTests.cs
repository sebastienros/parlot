using System;
using Xunit;

namespace Parlot.Tests;

public class WhitespaceVectorTests
{
    [Fact]
    public void PreservesEveryCharacterClassificationAfterVectorThreshold()
    {
        for (var code = 0; code <= char.MaxValue; code++)
        {
            var c = (char)code;
            var text = new string(' ', 17) + c + "x";
            var spaces = new Scanner(text);
            var lines = new Scanner(text);
            Assert.True(spaces.SkipWhiteSpace());
            Assert.True(lines.SkipWhiteSpaceOrNewLine());
            Assert.Equal(17 + (Character.IsWhiteSpace(c) ? 1 : 0), spaces.Cursor.Offset);
            var consumed = 17 + (Character.IsWhiteSpaceOrNewLine(c) ? 1 : 0);
            var expected = new Cursor(text);
            for (var i = 0; i < consumed; i++) expected.Advance();
            Assert.Equal(expected.Position, lines.Cursor.Position);
        }
    }

    [Fact]
    public void HandlesEmptyShortLongAndEntirelyWhitespaceInputs()
    {
        foreach (var count in new[] { 0, 1, 2, 7, 8, 9, 16, 256 })
        {
            var scanner = new Scanner(new string(' ', count));
            Assert.Equal(count != 0, scanner.SkipWhiteSpace());
            Assert.Equal(count, scanner.Cursor.Offset);
            Assert.True(scanner.Cursor.Eof);
            scanner = new Scanner(new string(' ', count) + "x");
            Assert.Equal(count != 0, scanner.SkipWhiteSpaceOrNewLine());
            var position = scanner.Cursor.Position;
            Assert.False(scanner.SkipWhiteSpaceOrNewLine());
            Assert.Equal(position, scanner.Cursor.Position);
        }
    }
}
