#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated lookup of a choice sends the non-ASCII chars without a case of their own to the alternatives that can
// start with any of them, like the runtime one.
public class NonAsciiSeekTests
{
    private static readonly string[] TokenInputs =
    [
        "", "if", "if (", "( ω!", "ω", "ωx", "ω!x", "αβγ if", "Жук (", "abc", "if€", "€", "#", "(é", "ж", "1", " ( ( ω! ",
    ];

    private static readonly string[] OtherInputs =
    [
        "", "if", "ifx", "#", "##", "ж", "жa", "ω", "Ω", "€", "é", "a", "1", " ", "\u0080", "￿",
    ];

    // The grammars of NonAsciiSeekGrammars.parlot.cs, at runtime
    // ASCII letters, and the Greek and Cyrillic ones
    private static readonly string Letters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
        + new string(Enumerable.Range(0x370, 0x500 - 0x370).Select(static i => (char)i).Where(char.IsLetter).ToArray());

    private static Parser<string> Tokens() =>
        OneOf(
            Terms.Text("if"),
            Terms.Char('(').Then(static _ => "("),
            Terms.Text("ω!").Then(static _ => "omega!"),
            Terms.AnyOf(Letters).Then(static x => x.ToString()));

    private static Parser<string> WithOther() =>
        OneOf(
            Literals.Text("if"),
            Literals.Pattern(static c => c == '#' || c == 'ж', maxSize: 1).Then(static x => "other:" + x.ToString()),
            Literals.AnyOf(Letters).Then(static x => x.ToString()),
            Literals.Text("€"));

    [Fact]
    public void GeneratedChoicesMatchTheRuntimeParser()
    {
        var tokens = ZeroOrMany(Tokens()).Eof();
        var withOther = WithOther();

        foreach (var input in TokenInputs)
        {
            var expected = tokens.TryParse(input, out var expectedValue);
            Assert.Equal(expected, NonAsciiSeekGrammars.TryParseTokens(input, out var value));
            Assert.Equal(expected, NonAsciiSeekGrammars.TryParseTokens(new StringReader(input), out var readerValue));

            if (expected)
            {
                Assert.Equal(expectedValue, value);
                Assert.Equal(expectedValue, readerValue);
            }
        }

        foreach (var input in OtherInputs)
        {
            var expected = withOther.TryParse(input, out var expectedValue);
            Assert.Equal(expected, NonAsciiSeekGrammars.TryParseWithOther(input, out var value));
            Assert.Equal(expected, NonAsciiSeekGrammars.TryParseWithOther(new StringReader(input), out var readerValue));

            if (expected)
            {
                Assert.Equal(expectedValue, value);
                Assert.Equal(expectedValue, readerValue);
            }
        }

        Assert.True(NonAsciiSeekGrammars.TryParseTokens("if ( ω! Жук", out var tokensValue));
        Assert.Equal(["if", "(", "omega!", "Жук"], tokensValue);
        Assert.False(NonAsciiSeekGrammars.TryParseTokens("if €", out _));

        Assert.True(NonAsciiSeekGrammars.TryParseWithOther("ж", out var otherValue));
        Assert.Equal("other:ж", otherValue);
        Assert.True(NonAsciiSeekGrammars.TryParseWithOther("Ω", out otherValue));
        Assert.Equal("Ω", otherValue);
        Assert.False(NonAsciiSeekGrammars.TryParseWithOther("é", out _));
        Assert.False(NonAsciiSeekGrammars.TryParseWithOther("1", out _));
    }

    [Fact]
    public void GeneratedLookupsHaveNoCaseForEachNonAsciiChar()
    {
        var source = string.Join("\n", ((ISourceable)WithOther()).GenerateSource(new SourceGenerationContext()).Body);

        Assert.Contains(">= '\\u0080'", source, StringComparison.Ordinal);
        Assert.Contains("'\\u20AC'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("'\\u03A9'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("'\\uFFFF'", source, StringComparison.Ordinal);
    }
}

public static partial class NonAsciiSeekGrammars
{
    public static partial bool TryParseTokens(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseTokens(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseWithOther(string text, out string value);
    public static partial bool TryParseWithOther(TextReader reader, out string value);
}
