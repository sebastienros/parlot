using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class TextBeforeFalseStartsTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1000)]
    [InlineData(true, 1000)]
    [InlineData(false, 4000)]
    [InlineData(true, 4000)]
    public void FindsDelimiterAfterFalseStarts(bool compiled, int length)
    {
        var prefix = new string('-', length).Replace("--", "a-");
        var parser = AnyCharBefore(Literals.Text("-->").Or(Literals.Text("==>")), canBeEmpty: true);
        if (compiled) parser = parser.Compile();

        Assert.True(parser.TryParse(prefix + "-->", out var value));
        Assert.Equal(prefix, value.ToString());
        Assert.True(parser.TryParse(prefix + "==>", out value));
        Assert.Equal(prefix, value.ToString());
        Assert.True(parser.TryParse(prefix, out value));
        Assert.Equal(prefix, value.ToString());
    }
}
