using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class GrammarsTests
{
    [Fact]
    public void ParserWithNoName_GeneratesProperty()
    {
        var parser = Grammars.ParserWithNoName_Parser;

        var result = parser.Parse("hello");
        Assert.Equal("hello", result);

        result = parser.Parse("world");
        Assert.Null(result);
    }

    [Fact]
    public void HelloParser_GeneratesProperty()
    {
        var parser = Grammars.Hello;

        var result = parser.Parse("hello");
        Assert.Equal("hello", result);

        result = parser.Parse("world");
        Assert.Null(result);
    }
}