using System;
using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public class TextChoiceSourceTests
{
    [Fact]
    public void OnlyCompatibleTextChoicesUsePackedPrefixRecognition()
    {
        var compatible = new[] { "if1", "if", "if12", "Content-Encoding", "Content", "\u00e9", "line\r\nend", "\0" }
            .Select(static word => Literals.Text(word)).ToArray();
        AssertOptimized(OneOf(compatible), true);
        AssertOptimized(compatible.Aggregate(static (left, right) => left.Or(right)), true);
        AssertOptimized(OneOf(compatible[..4]).Or(OneOf(compatible[4..])), true);
        AssertOptimized(OneOf(compatible.Select(static parser => SkipWhiteSpace(parser)).ToArray()), true);
        foreach (var replacement in new Parser<string>[]
        {
            Literals.Text(""), Literals.Text(new string('a', 65)), Literals.Text("IF", caseInsensitive: true),
            new TextLiteral("if", StringComparison.InvariantCulture), Literals.Keyword("if"), Terms.Text("if"),
            Literals.Text("if").Then(static value => value),
        })
        {
            AssertOptimized(OneOf([replacement, .. compatible[1..]]), false);
        }

        AssertOptimized(OneOf(compatible[..4]), false);
        AssertOptimized(OneOf(Enumerable.Repeat(compatible[0], 257).ToArray()), false);
        AssertOptimized(OneOf(Enumerable.Range(0, 100).Select(static index => Literals.Text(new string('a', 42))).ToArray()), false);
    }

    private static void AssertOptimized(Parser<string> parser, bool expected)
    {
        var source = ((ISourceable)parser).GenerateSource(new SourceGenerationContext());
        Assert.Equal(expected, string.Join("\n", source.Body).Contains("MatchText", StringComparison.Ordinal));
    }
}
