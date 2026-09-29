using System;
using System.Collections.Generic;
using Parlot.Fluent;
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
    [InlineData("x", true, true, true, true)]
    [InlineData("x,x", true, true, true, true)]
    [InlineData("x,,x", true, false, false, true)]
    [InlineData(",x", false, true, false, true)]
    [InlineData("x,", false, false, true, true)]
    [InlineData(",,x,,x,,", false, false, false, true)]
    [InlineData(",,x", false, false, false, true)]
    [InlineData("x,,", false, false, false, true)]
    [InlineData("", false, false, false, false)]
    [InlineData(",,", false, false, false, false)]
    public void Separated_Options_Match_Runtime(
        string input, bool interior, bool leading, bool trailing, bool all)
    {
        var expected = new[]
        {
            SeparatedResult(input, removeEmptyEntries: true),
            SeparatedResult(input, allowLeadingSeparator: true),
            SeparatedResult(input, allowTrailingSeparator: true),
            SeparatedResult(input, removeEmptyEntries: true, allowLeadingSeparator: true, allowTrailingSeparator: true)
        };

        Assert.Equal(expected[0].Success, CollectionGrammars.TryParseSeparatedInterior(input, out var interiorValues));
        Assert.Equal(expected[1].Success, CollectionGrammars.TryParseSeparatedLeading(input, out var leadingValues));
        Assert.Equal(expected[2].Success, CollectionGrammars.TryParseSeparatedTrailing(input, out var trailingValues));
        Assert.Equal(expected[3].Success, CollectionGrammars.TryParseSeparatedAllOptions(input, out var allValues));
        Assert.Equal(interior, expected[0].Success);
        Assert.Equal(leading, expected[1].Success);
        Assert.Equal(trailing, expected[2].Success);
        Assert.Equal(all, expected[3].Success);
        if (all)
        {
            Assert.Equal(expected[3].Values, allValues);
        }
        if (interior)
        {
            Assert.Equal(expected[0].Values, interiorValues);
        }
        if (leading)
        {
            Assert.Equal(expected[1].Values, leadingValues);
        }
        if (trailing)
        {
            Assert.Equal(expected[2].Values, trailingValues);
        }
    }

    [Theory]
    [InlineData(",,x,,?", true)]
    [InlineData(",,?", false)]
    public void Separated_Options_Work_When_Result_Is_Discarded(string input, bool expected)
    {
        Assert.Equal(expected, CollectionGrammars.TryParseSeparatedDiscarded(input, out var value));
        if (expected)
        {
            Assert.Equal('?', value);
        }
    }

    [Fact]
    public void Separated_Options_Restore_Cursor_For_Following_Parsers()
    {
        Assert.True(CollectionGrammars.TryParseSeparatedUnmatchedRun("x,,?", out var count));
        Assert.Equal(1, count);
        Assert.True(CollectionGrammars.TryParseSeparatedLeadingFallback(",", out var fallback));
        Assert.Equal(-1, fallback);
    }

    [Fact]
    public void Separated_Options_Stop_At_Zero_Progress()
    {
        Assert.True(CollectionGrammars.TryParseSeparatedOptionalOptions("aaa", out var values));
        Assert.Equal(3, values.Count);
        Assert.False(CollectionGrammars.TryParseSeparatedOptionalOptions("", out _));
        Assert.False(CollectionGrammars.TryParseSeparatedOptionalOptions(",", out _));
    }

    private static (bool Success, IReadOnlyList<string> Values) SeparatedResult(
        string input, bool removeEmptyEntries = false, bool allowLeadingSeparator = false, bool allowTrailingSeparator = false)
    {
        var parser = Parlot.Fluent.Parsers.Separated(Parlot.Fluent.Parsers.Literals.Char(','),
            Parlot.Fluent.Parsers.Literals.Text("x"), removeEmptyEntries, allowLeadingSeparator, allowTrailingSeparator).Eof();
        var success = parser.TryParse(input, out var values);
        return (success, values);
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
    public static partial bool TryParseSeparatedInterior(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedLeading(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedTrailing(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedAllOptions(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedDiscarded(string text, out char value);
    public static partial bool TryParseSeparatedUnmatchedRun(string text, out int value);
    public static partial bool TryParseSeparatedLeadingFallback(string text, out int value);
    public static partial bool TryParseSeparatedOptionalOptions(string text, out IReadOnlyList<char> value);
    public static partial bool TryParseTuples(string text, out IReadOnlyList<(int Number, string Text)> value);
    public static partial bool TryParseSeparatedThenQuestion(string text, out int value);
    public static partial bool TryParseCapturedZero(string text, out string value);
}
