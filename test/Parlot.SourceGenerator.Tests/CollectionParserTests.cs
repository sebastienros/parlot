using System;
using System.Collections.Generic;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class CollectionParserTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(32)]
    public void ZeroOrMany_Uses_Embedded_Collection_Storage(int count)
    {
        var input = new string('x', count);

        Assert.True(CollectionGrammars.TryParseZeroStrings(input, out var values));
        Assert.Equal(count, values.Count);

        if (count == 0)
        {
            Assert.Same(Array.Empty<string>(), values);
        }
        else if (count <= 4)
        {
            Assert.Equal("Parlot.Generated.Fluent.HybridList`1", values.GetType().GetGenericTypeDefinition().FullName);
        }
        else
        {
            Assert.IsType<List<string>>(values);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(32)]
    public void OneOrMany_And_Separated_Preserve_All_Items(int count)
    {
        var adjacent = new string('x', count);
        var separated = string.Join(",", new string('x', count).ToCharArray());

        Assert.True(CollectionGrammars.TryParseOneStrings(adjacent, out var oneOrMany));
        Assert.True(CollectionGrammars.TryParseSeparatedStrings(separated, out var separatedValues));
        Assert.Equal(count, oneOrMany.Count);
        Assert.Equal(count, separatedValues.Count);
        Assert.All(oneOrMany, static value => Assert.Equal("x", value));
        Assert.All(separatedValues, static value => Assert.Equal("x", value));
    }

    [Fact]
    public void OneOrMany_And_Separated_Reject_Empty_Input()
    {
        Assert.False(CollectionGrammars.TryParseOneStrings("", out var oneOrMany));
        Assert.Null(oneOrMany);
        Assert.False(CollectionGrammars.TryParseSeparatedStrings("", out var separated));
        Assert.Null(separated);
    }

    [Fact]
    public void Tuple_Collections_Remain_Bcl_Shaped()
    {
        Assert.True(CollectionGrammars.TryParseTuples("xxx", out var values));
        Assert.Equal(new[] { (1, "x"), (1, "x"), (1, "x") }, values);
    }

    [Theory]
    [InlineData("x,?", 1)]
    [InlineData("x,x,?", 2)]
    public void Separated_Rolls_Back_A_Trailing_Separator(string input, int expectedCount)
    {
        Assert.True(CollectionGrammars.TryParseSeparatedThenQuestion(input, out var count));
        Assert.Equal(expectedCount, count);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("x", "x")]
    [InlineData("xxxx", "xxxx")]
    public void Discarded_Collections_Can_Be_Captured_As_String(string input, string expected)
    {
        Assert.True(CollectionGrammars.TryParseCapturedZero(input, out var value));
        Assert.Equal(expected, value);
    }
}

public static partial class CollectionGrammars
{
    public static partial bool TryParseZeroStrings(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseOneStrings(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedStrings(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseTuples(string text, out IReadOnlyList<(int Number, string Text)> value);
    public static partial bool TryParseSeparatedThenQuestion(string text, out int value);
    public static partial bool TryParseCapturedZero(string text, out string value);
}
