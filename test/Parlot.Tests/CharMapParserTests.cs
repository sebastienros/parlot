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
    [InlineData("a")]
    [InlineData("\u00e9a")]
    [InlineData("\u00e9b")]
    [InlineData("\u4e2d")]
    public void OneOfShouldMatchAsciiAndNonAsciiBranches(string input)
    {
        var parser = OneOf(
            Literals.Text("a"),
            Literals.Text("\u00e9a"),
            Literals.Text("\u00e9b"),
            Literals.Text("\u4e2d"));

        Assert.True(parser.TryParse(input, out var value));
        Assert.Equal(input, value);
    }

    [Theory]
    [InlineData("z")]
    [InlineData("\u4e2d")]
    [InlineData("\u00e9x")]
    public void OneOfShouldRestorePositionWhenLookupOrBranchFails(string input)
    {
        var parser = OneOf(Literals.Text("a"), Literals.Text("\u00e9a"), Literals.Text("\u00e9b"));
        var context = new ParseContext(new Scanner("!" + input));
        context.Scanner.Cursor.Advance();
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<string>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
    }

    [Fact]
    public void OneOfShouldPreserveBranchOrderForDuplicateNonAsciiKeys()
    {
        var parser = OneOf(Literals.Text("\u00e9").Then(1), Literals.Text("\u00e9").Then(2));

        Assert.Equal(1, parser.Parse("\u00e9"));
    }

    [Fact]
    public void AnyOfShouldHandleDuplicateNonAsciiCharacters()
    {
        var parser = Literals.AnyOf("a\u00e9\u00e9\u4e2d");

        Assert.Equal("a\u00e9\u4e2d\u00e9", parser.Parse("a\u00e9\u4e2d\u00e9!").ToString());
        Assert.False(parser.TryParse("\u00f1", out _));
    }

    [Fact]
    public void NoneOfShouldHandleDuplicateNonAsciiCharacters()
    {
        var parser = Literals.NoneOf("a\u00e9\u00e9\u4e2d");

        Assert.Equal("z\u00f1", parser.Parse("z\u00f1\u00e9").ToString());
        Assert.False(parser.TryParse("\u4e2d", out _));
    }
}
