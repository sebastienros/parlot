using Xunit;

namespace Parlot.Tests;

public class CharacterBoundaryTests
{
    [Fact]
    public void ClassifiesTheLastUtf16CodeUnitAndPreservesFormFeed()
    {
        Assert.False(Character.IsWhiteSpace(char.MaxValue));
        Assert.False(Character.IsWhiteSpaceOrNewLine(char.MaxValue));
        Assert.False(Character.IsIdentifierStart(char.MaxValue));
        Assert.False(Character.IsIdentifierPart(char.MaxValue));
        Assert.True(Character.IsWhiteSpace('\f'));
        Assert.True(Character.IsWhiteSpaceOrNewLine('\f'));
        var scanner = new Scanner(" \uffff");
        Assert.True(scanner.SkipWhiteSpace());
        Assert.Equal(1, scanner.Cursor.Offset);
        Assert.False(scanner.SkipWhiteSpace());
    }
}
