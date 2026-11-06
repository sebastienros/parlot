using System.Collections.Generic;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// A generated callback receives the offsets the runtime one does when its parser starts by skipping white space.
/// </summary>
public class ThenOffsetsTests
{
    private delegate bool TryParse(string text, out (int Start, int End) value);

    private static readonly Dictionary<string, (Parser<(int, int)> Runtime, TryParse Generated)> Grammars = new()
    {
        ["choice"] = (Terms.Text("ab").Or(Terms.Text("cd")).Then(static (context, start, end, _) => (start, end)), PositionGrammars.TryParseOffsetsChoice),
        ["large-choice"] = (OneOf(
            Terms.Keyword("if"), Terms.Keyword("else"), Terms.Keyword("for"), Terms.Keyword("while"),
            Terms.Keyword("do"), Terms.Keyword("return"), Terms.Keyword("break"), Terms.Keyword("continue"))
            .Then(static (context, start, end, _) => (start, end)), PositionGrammars.TryParseOffsetsLargeChoice),
        ["sequence"] = (Terms.Text("ab").And(Terms.Text("cd")).Then(static (context, start, end, _) => (start, end)), PositionGrammars.TryParseOffsetsSequence),
        ["between"] = (Between(Terms.Char('['), Terms.Identifier(), Terms.Char(']')).Then(static (context, start, end, _) => (start, end)), PositionGrammars.TryParseOffsetsBetween),
        ["between-literals"] = (Between(Literals.Char('['), Literals.Identifier(), Literals.Char(']')).Then(static (context, start, end, _) => (start, end)), PositionGrammars.TryParseOffsetsBetweenLiterals),
        ["skipand-andskip"] = (Terms.Char('[').SkipAnd(Terms.Identifier()).AndSkip(Terms.Char(']')).Then(static (context, start, end, _) => (start, end)), PositionGrammars.TryParseOffsetsSkipAndAndSkip),
    };

    [Theory]
    [InlineData("choice", "ab", 0, 2)]
    [InlineData("choice", "  ab", 2, 4)]
    [InlineData("choice", "\n\tcd", 2, 4)]
    [InlineData("choice", "  xx", -1, 0)]
    [InlineData("large-choice", "if", 0, 2)]
    [InlineData("large-choice", "  continue", 2, 10)]
    [InlineData("large-choice", "  iffy", -1, 0)]
    [InlineData("sequence", "ab cd", 0, 5)]
    [InlineData("sequence", "  ab  cd", 2, 8)]
    [InlineData("sequence", "  ab  x", -1, 0)]
    [InlineData("between", "[x]", 0, 3)]
    [InlineData("between", " [ xy ] ", 1, 7)]
    [InlineData("between", " [ xy ", -1, 0)]
    [InlineData("between", " xy ]", -1, 0)]
    [InlineData("between-literals", "[xy]", 0, 4)]
    [InlineData("between-literals", "[xy", -1, 0)]
    [InlineData("skipand-andskip", "[x]", 0, 3)]
    [InlineData("skipand-andskip", " [ xy ] ", 1, 7)]
    [InlineData("skipand-andskip", " [ xy ", -1, 0)]
    public void Generated_Callback_Receives_The_Start_Of_The_Content(string grammar, string input, int start, int end)
    {
        var (runtime, generated) = Grammars[grammar];

        if (start < 0)
        {
            Assert.False(runtime.TryParse(input, out _));
            Assert.False(generated(input, out _));
            return;
        }

        Assert.True(runtime.TryParse(input, out var expected));
        Assert.Equal((start, end), expected);
        Assert.True(generated(input, out var value));
        Assert.Equal((start, end), value);
    }

    [Fact]
    public void Generated_Choice_That_Doesnt_Always_Skip_Reports_Where_It_Was_Entered()
    {
        Assert.True(PositionGrammars.TryParseOffsetsMixedChoice("  ab", out var value));
        Assert.Equal((0, 4), value);
    }

    [Theory]
    [InlineData("  x", 0, 3)]
    [InlineData("  ab  x", 4, 7)]
    public void Failed_Choice_Gives_The_White_Space_Back(string input, int start, int end)
    {
        Assert.True(PositionGrammars.TryParseOffsetsBacktrack(input, out var value));
        Assert.Equal((start, end), value);
    }
}

public static partial class PositionGrammars
{
    public static partial bool TryParseOffsetsChoice(string text, out (int Start, int End) value);
    public static partial bool TryParseOffsetsLargeChoice(string text, out (int Start, int End) value);
    public static partial bool TryParseOffsetsSequence(string text, out (int Start, int End) value);
    public static partial bool TryParseOffsetsMixedChoice(string text, out (int Start, int End) value);
    public static partial bool TryParseOffsetsBacktrack(string text, out (int Start, int End) value);
    public static partial bool TryParseOffsetsBetween(string text, out (int Start, int End) value);
    public static partial bool TryParseOffsetsBetweenLiterals(string text, out (int Start, int End) value);
    public static partial bool TryParseOffsetsSkipAndAndSkip(string text, out (int Start, int End) value);
}
