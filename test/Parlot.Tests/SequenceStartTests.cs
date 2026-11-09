#nullable enable

using Parlot.Fluent;
using System.Collections.Generic;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// A sequence starts where the first of its parts which matched something does, after the white space the next part skips
/// when the first one matched nothing.
/// </summary>
public class SequenceStartTests
{
    private static readonly Parser<IReadOnlyList<string>> Decorators = ZeroOrMany(Terms.Char('@').SkipAnd(Terms.Identifier().Then(static x => x.ToString()!)));
    private static readonly Parser<Option<char>> OptionalStar = Terms.Char('*').Optional();
    private static readonly Parser<char> StarElse = Terms.Char('*').Else('-');
    private static readonly Parser<object?> Nothing = Always();
    private static readonly Parser<string> Class = Terms.Text("class");
    private static readonly Parser<string> Name = Terms.Identifier().Then(static x => x.ToString()!);

    private static readonly Dictionary<string, Parser<(int Start, int End)>> Grammars = new()
    {
        ["zeroormany"] = Offsets(Decorators.And(Class)),
        ["optional"] = Offsets(OptionalStar.And(Class)),
        ["always"] = Offsets(Nothing.And(Class)),
        ["else"] = Offsets(StarElse.And(Class)),
        ["not"] = Offsets(Not(Terms.Char('!')).And(Class)),
        ["zeroormany-3"] = Offsets(Decorators.And(Class).And(Name)),
        ["optional-3"] = Offsets(OptionalStar.And(Class).And(Name)),
        ["always-3"] = Offsets(Nothing.And(Class).And(Name)),
        ["else-3"] = Offsets(StarElse.And(Class).And(Name)),
        ["two-empty-3"] = Offsets(Decorators.And(OptionalStar).And(Class)),
        ["two-empty-7"] = Offsets(Decorators.And(OptionalStar).And(Nothing).And(Class).And(Name).And(OptionalStar).And(Name)),
        ["nested"] = Offsets(Decorators.And(OptionalStar.And(Class))),
        ["skipand"] = Offsets(Decorators.SkipAnd(Class)),
        ["skipand-3"] = Offsets(Decorators.And(OptionalStar).SkipAnd(Class)),
        ["andskip"] = Offsets(Decorators.AndSkip(Class)),
        ["andskip-3"] = Offsets(Decorators.And(OptionalStar).AndSkip(Class)),
        ["all-empty"] = Offsets(Decorators.And(OptionalStar)),
        ["all-empty-3"] = Offsets(Decorators.And(OptionalStar).And(Nothing)),
    };

    private static Parser<(int, int)> Offsets<T>(Parser<T> parser) => parser.Then(static (_, start, end, _) => (start, end));

    [Theory]
    [InlineData("zeroormany", "class", 0, 5)]
    [InlineData("zeroormany", "   class", 3, 8)]
    [InlineData("zeroormany", "   @a class", 3, 11)]
    [InlineData("zeroormany", "  @a  @b class", 2, 14)]
    [InlineData("optional", "   class", 3, 8)]
    [InlineData("optional", "   * class", 3, 10)]
    [InlineData("always", "   class", 3, 8)]
    [InlineData("else", "   class", 3, 8)]
    [InlineData("else", "  *class", 2, 8)]
    [InlineData("not", "   class", 3, 8)]
    [InlineData("zeroormany-3", "   class a", 3, 10)]
    [InlineData("zeroormany-3", "   @a class b", 3, 13)]
    [InlineData("optional-3", "   class a", 3, 10)]
    [InlineData("optional-3", " * class a", 1, 10)]
    [InlineData("always-3", "   class a", 3, 10)]
    [InlineData("else-3", "   class a", 3, 10)]
    [InlineData("two-empty-3", "   class", 3, 8)]
    [InlineData("two-empty-3", "   *  class", 3, 11)]
    [InlineData("two-empty-3", "  @a  * class", 2, 13)]
    [InlineData("two-empty-7", "   class a b", 3, 12)]
    [InlineData("two-empty-7", "  * class a * b", 2, 15)]
    [InlineData("nested", "   class", 3, 8)]
    [InlineData("nested", "   *  class", 3, 11)]
    [InlineData("skipand", "   class", 3, 8)]
    [InlineData("skipand", "  @a class", 2, 10)]
    [InlineData("skipand-3", "   class", 3, 8)]
    [InlineData("andskip", "   class", 3, 8)]
    [InlineData("andskip-3", "   class", 3, 8)]
    [InlineData("andskip-3", " * class", 1, 8)]
    // Nothing matched: the sequence is empty where it was invoked
    [InlineData("all-empty", "   x", 0, 0)]
    [InlineData("all-empty-3", "   x", 0, 0)]
    public void ShouldStartAtTheFirstPartWhichMatched(string grammar, string input, int start, int end)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<(int, int)>();

        Assert.True(Grammars[grammar].Parse(context, ref result));
        Assert.Equal((start, end), result.Value);
        Assert.Equal(start, result.Start);
        Assert.Equal(end, result.End);

        // Only the reported start changes, the cursor is after the match
        Assert.Equal(end, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("zeroormany", "   struct")]
    [InlineData("zeroormany", "   @a struct")]
    [InlineData("optional", "   * struct")]
    [InlineData("always", "   struct")]
    [InlineData("else", "   * struct")]
    [InlineData("zeroormany-3", "   @a class ")]
    [InlineData("optional-3", "   class 1")]
    [InlineData("two-empty-7", "  * class a * ")]
    [InlineData("nested", "   * struct")]
    [InlineData("skipand-3", "   * struct")]
    [InlineData("andskip-3", "   * struct")]
    public void ShouldRestoreTheCursorOnFailure(string grammar, string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<(int, int)>();

        Assert.False(Grammars[grammar].Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldStartAtTheFirstPartWhenItMatched()
    {
        // A part which matched something keeps its start, even when it is the one of its own content
        var parser = Literals.WhiteSpace().And(Class);
        var context = new ParseContext(new Scanner("  class"));
        var result = new ParseResult<(TextSpan, string)>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(0, result.Start);
        Assert.Equal(7, result.End);
    }
}
