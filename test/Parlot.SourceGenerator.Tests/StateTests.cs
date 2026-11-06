#nullable enable

using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated parsers set and restore the state of WithState like the runtime ones.
public class StateTests
{
    [Theory]
    [InlineData("yield", true, "identifier:yield")]
    [InlineData("*{yield, a}", true, "keyword|identifier:a")]
    [InlineData("*{ yield , yield }", true, "keyword|keyword")]
    [InlineData("a, yield", true, "identifier:a|identifier:yield")]
    [InlineData("*{yield", false, null)]
    [InlineData("*{a b}", false, null)]
    public void Generated_Parser_Sees_The_State(string text, bool success, string? expected)
    {
        var options = new StateOptions();
        Assert.Equal(success, StateGrammars.TryParseGenerator(text, options, out var value));
        Assert.Equal(expected, value);
        Assert.False(options.Generator);

        Assert.Equal(success, StateGrammars.TryParseGenerator(new StringReader(text), options, out value));
        Assert.Equal(expected, value);
        Assert.False(options.Generator);

        // The runtime parser parses the same
        var runtime = new StateOptions();
        var parser = Generator(runtime);
        Assert.Equal(success, parser.TryParse(text, out value));
        Assert.Equal(expected, value);
        Assert.False(runtime.Generator);
    }

    [Theory]
    [InlineData("x", 0)]
    [InlineData("(x)", 1)]
    [InlineData("((( x )))", 3)]
    public void Generated_Parser_Nests_The_State(string text, int depth)
    {
        var options = new StateOptions();
        Assert.True(StateGrammars.TryParseDepth(text, options, out var value));
        Assert.Equal(depth, value);
        Assert.Equal(0, options.Depth);

        Assert.True(StateGrammars.TryParseDepth(new StringReader(text), options, out value));
        Assert.Equal(depth, value);
        Assert.Equal(0, options.Depth);
    }

    [Theory]
    [InlineData("((x)")]
    [InlineData("(y)")]
    [InlineData("(x))")]
    public void Generated_Parser_Restores_The_State_On_Failure_And_Errors(string text)
    {
        var options = new StateOptions();
        Assert.False(StateGrammars.TryParseDepth(text, options, out _));
        Assert.Equal(0, options.Depth);

        Assert.False(StateGrammars.TryParseDepth(new StringReader(text), options, out _));
        Assert.Equal(0, options.Depth);
    }

    [Fact]
    public void Generated_Parser_Sets_The_State_Of_A_Discarded_Value()
    {
        lock (StaticState.Lock)
        {
            Assert.True(StateGrammars.TryParseDiscarded("a b", out var value));
            Assert.Equal('a', value);
            Assert.Equal(0, StaticState.Value);

            Assert.False(StateGrammars.TryParseDiscarded("a c", out _));
            Assert.Equal(0, StaticState.Value);
        }
    }

    private static Parser<string> Generator(StateOptions options) =>
        OneOf(
            Between(
                Terms.Text("*{"),
                Separated(
                    Terms.Char(','),
                    If(
                        () => options.Generator,
                        OneOf(Terms.Text("yield").Then(static _ => "keyword"), Terms.Identifier().Then(static x => "identifier:" + x.ToString())),
                        Terms.Identifier().Then(static x => "identifier:" + x.ToString())))
                .WithState(() => options.Generator, value => options.Generator = value, static _ => true),
                Terms.Char('}')),
            Separated(
                Terms.Char(','),
                If(
                    () => options.Generator,
                    Terms.Text("yield").Then(static _ => "keyword"),
                    Terms.Identifier().Then(static x => "identifier:" + x.ToString()))))
        .Then(static list => string.Join("|", list))
        .Eof();
}

public sealed class StateOptions
{
    public bool Generator { get; set; }

    public int Depth { get; set; }
}

public static class StaticState
{
    public static readonly object Lock = new();

    public static int Value;
}

public static partial class StateGrammars
{
    public static partial bool TryParseGenerator(string text, StateOptions options, out string value);
    public static partial bool TryParseGenerator(TextReader reader, StateOptions options, out string value);
    public static partial bool TryParseDepth(string text, StateOptions options, out int value);
    public static partial bool TryParseDepth(TextReader reader, StateOptions options, out int value);
    public static partial bool TryParseDiscarded(string text, out char value);
}
