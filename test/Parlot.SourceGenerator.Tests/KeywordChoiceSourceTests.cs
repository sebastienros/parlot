using System;
using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public class KeywordChoiceSourceTests
{
    [Fact]
    public void OnlyCompatibleKeywordChoicesUseExactTokenRecognition()
    {
        var compatible = new[] { "if", "else", "while", "return", "int", "internal", "interface", "class" }
            .Select(static word => Literals.Keyword(word)).ToArray();
        AssertOptimized(OneOf(compatible), true);
        AssertOptimized(compatible.Aggregate(static (left, right) => left.Or(right)), true);
        AssertOptimized(OneOf(compatible[..4]).Or(OneOf(compatible[4..])), true);
        foreach (var replacement in new Parser<string>[]
        {
            Literals.Keyword(""), Literals.Keyword("if1"), Literals.Keyword("Content-Type"),
            Literals.Keyword("\u00e9"), Literals.Keyword("two\nlines"), Literals.Keyword(new string('a', 65)),
            Literals.Keyword("IF", caseInsensitive: true), new KeywordLiteral("if", StringComparison.InvariantCulture),
            Literals.Text("if"), Terms.Keyword("if"),
        })
        {
            AssertOptimized(OneOf([replacement, .. compatible[1..]]), false);
        }

        AssertOptimized(OneOf(compatible.Take(2).ToArray()), false);
        AssertOptimized(OneOf(compatible.Take(4).ToArray()), false);
        AssertOptimized(OneOf(Enumerable.Repeat(compatible[0], 257).ToArray()), false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedKeywordChoicesRestoreTheEntireCursorPosition(bool terms)
    {
        var parser = OneOf(new[] { "if", "else", "while", "return", "int", "internal", "interface", "class" }
            .Select(word => terms ? Terms.Keyword(word) : Literals.Keyword(word)).ToArray());
        var context = new ParseContext(new Scanner("! \r\nunknown"));
        context.Scanner.Cursor.Advance();
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<string>();
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
    }

    [Fact]
    public void DigitAndPunctuationKeywordsRemainOrderedRatherThanLongest()
    {
        var parser = OneOf(Literals.Keyword("if1"), Literals.Keyword("if12"), Literals.Keyword("if-else"), Literals.Keyword("if"));
        AssertOptimized(parser, false);
        Assert.Equal("if1", parser.Parse("if12"));
        Assert.Equal("if-else", parser.Parse("if-else"));
        Assert.Equal("if", parser.Parse("if_else"));
    }

    private static void AssertOptimized(Parser<string> parser, bool expected)
    {
        var source = ((ISourceable)parser).GenerateSource(new SourceGenerationContext());
        Assert.Equal(expected, string.Join("\n", source.Body).IndexOf("MatchKeyword", StringComparison.Ordinal) >= 0);
    }
}
