using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class CharMapParserTests
{
#if !NET10_0_OR_GREATER
    [Fact]
    public void RuntimeShouldNotReferenceImmutableCollections()
    {
        Assert.DoesNotContain(typeof(Parser<char>).Assembly.GetReferencedAssemblies(),
            static assembly => assembly.Name == "System.Collections.Immutable");
    }
#endif

    [Theory]
    [InlineData("a", false)]
    [InlineData("a", true)]
    [InlineData("\u00e9a", false)]
    [InlineData("\u00e9a", true)]
    [InlineData("\u00e9b", false)]
    [InlineData("\u00e9b", true)]
    [InlineData("\u4e2d", false)]
    [InlineData("\u4e2d", true)]
    public void OneOfShouldMatchAsciiAndNonAsciiBranches(string input, bool compiled)
    {
        var parser = OneOf(
            Literals.Text("a"),
            Literals.Text("\u00e9a"),
            Literals.Text("\u00e9b"),
            Literals.Text("\u4e2d"));

        if (compiled)
        {
            parser = parser.Compile();
        }

        Assert.True(parser.TryParse(input, out var value));
        Assert.Equal(input, value);
    }

    [Theory]
    [InlineData("z", false)]
    [InlineData("z", true)]
    [InlineData("\u4e2d", false)]
    [InlineData("\u4e2d", true)]
    [InlineData("\u00e9x", false)]
    [InlineData("\u00e9x", true)]
    public void OneOfShouldRestorePositionWhenLookupOrBranchFails(string input, bool compiled)
    {
        var parser = OneOf(Literals.Text("a"), Literals.Text("\u00e9a"), Literals.Text("\u00e9b"));
        if (compiled)
        {
            parser = parser.Compile();
        }

        var context = new ParseContext(new Scanner("!" + input));
        context.Scanner.Cursor.Advance();
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<string>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
    }

    [Theory]
    [InlineData("a", false)]
    [InlineData("a", true)]
    [InlineData("\u00e9", false)]
    [InlineData("\u00e9", true)]
    public void OneOfShouldPreserveBranchOrderForDuplicateKeys(string input, bool compiled)
    {
        var parser = OneOf(Literals.Text(input).Then(1), Literals.Text(input).Then(2));
        if (compiled)
        {
            parser = parser.Compile();
        }

        Assert.Equal(1, parser.Parse(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnyOfShouldHandleDuplicateNonAsciiCharacters(bool compiled)
    {
        var parser = Literals.AnyOf("a\u00e9\u00e9\u4e2d");
        if (compiled)
        {
            parser = parser.Compile();
        }

        Assert.Equal("a\u00e9\u4e2d\u00e9", parser.Parse("a\u00e9\u4e2d\u00e9!").ToString());
        Assert.False(parser.TryParse("\u00f1", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoneOfShouldHandleDuplicateNonAsciiCharacters(bool compiled)
    {
        var parser = Literals.NoneOf("a\u00e9\u00e9\u4e2d");
        if (compiled)
        {
            parser = parser.Compile();
        }

        Assert.Equal("z\u00f1", parser.Parse("z\u00f1\u00e9").ToString());
        Assert.False(parser.TryParse("\u4e2d", out _));
    }
}
