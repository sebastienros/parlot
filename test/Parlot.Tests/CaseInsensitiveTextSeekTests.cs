using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

// A case-insensitive text declares the chars it can start with. OneOf uses them to pick the alternatives to try
// and AnyCharBefore to jump to its delimiter, so they have to be the chars the comparison takes, whichever culture
// is current when the parser is built and when it parses. The result must be the one obtained without the lookup.
public class CaseInsensitiveTextSeekTests
{
    private sealed class Culture : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public Culture(string name)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previous;
        }
    }

    // Hides the seekable properties of a parser
    private static Parser<T> NotSeekable<T>(Parser<T> parser)
    {
        var deferred = Deferred<T>();
        deferred.Parser = parser;

        Assert.False(((ISeekable)deferred).CanSeek);

        return deferred;
    }

    // The chars an ordinal case-insensitive comparison takes for a char, found by asking it about every one
    private static char[] OrdinalIgnoreCaseChars(char c)
    {
        var expected = c.ToString();
        var chars = new List<char>();

        for (var i = 0; i <= char.MaxValue; i++)
        {
            if (string.Equals(((char)i).ToString(), expected, StringComparison.OrdinalIgnoreCase))
            {
                chars.Add((char)i);
            }
        }

        return [.. chars];
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    [InlineData("en-US")]
    [InlineData("")]
    public void ShouldMatchAnyCaseWhicheverCultureTheParserIsBuiltUnder(string culture)
    {
        Parser<string> lower, upper, keyword;

        // Under a Turkish culture the upper case of 'i' is the dotted 'I' (U+0130) and the lower case of 'I' is
        // the dotless 'i' (U+0131)
        using (new Culture(culture))
        {
            lower = Literals.Text("id", caseInsensitive: true).Or(Literals.Text("x"));
            upper = Literals.Text("ID", caseInsensitive: true).Or(Literals.Text("x"));
            keyword = Literals.Keyword("if", caseInsensitive: true).Or(Literals.Keyword("x"));

            AssertMatches();
        }

        AssertMatches();

        void AssertMatches()
        {
            foreach (var text in new[] { "id", "ID", "Id", "iD" })
            {
                Assert.True(lower.TryParse(text, out var result), text);
                Assert.Equal("id", result);

                Assert.True(upper.TryParse(text, out result), text);
                Assert.Equal("ID", result);
            }

            foreach (var text in new[] { "if", "IF", "If", "iF" })
            {
                Assert.True(keyword.TryParse(text, out var result), text);
                Assert.Equal("if", result);
            }

            // The Turkish letters are other letters to an ordinal comparison
            Assert.False(lower.TryParse("\u0130d", out _));
            Assert.False(lower.TryParse("\u0131d", out _));
            Assert.False(upper.TryParse("\u0130D", out _));
            Assert.False(upper.TryParse("\u0131D", out _));
        }
    }

    [Theory]
    [InlineData("tr-TR", "abcID", "abc")]
    [InlineData("tr-TR", "abcid", "abc")]
    [InlineData("tr-TR", "a\u0130b\u0131cId", "a\u0130b\u0131c")]
    [InlineData("tr-TR", "abc", "abc")]
    [InlineData("en-US", "abcID", "abc")]
    public void ShouldFindADelimiterInAnyCase(string culture, string text, string expected)
    {
        Parser<TextSpan> seekable, notSeekable;

        using (new Culture(culture))
        {
            seekable = AnyCharBefore(Literals.Text("id", caseInsensitive: true));
            notSeekable = AnyCharBefore(NotSeekable(Literals.Text("id", caseInsensitive: true)));
        }

        Assert.True(seekable.TryParse(text, out var result));
        Assert.Equal(expected, result.ToString());

        Assert.True(notSeekable.TryParse(text, out result));
        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData('i')]
    [InlineData('I')]
    [InlineData('a')]
    [InlineData('Z')]
    [InlineData('k')]
    [InlineData('s')]
    [InlineData('1')]
    [InlineData('_')]
    [InlineData('\u00E9')] // e acute
    [InlineData('\u00C9')] // E acute
    [InlineData('\u0436')] // Cyrillic zhe
    [InlineData('\u03C3')] // Greek sigma, which has a second lower case
    [InlineData('\u03C2')] // Greek final sigma
    [InlineData('\u03A3')] // Greek capital sigma
    [InlineData('\u00B5')] // The micro sign, which shares its upper case with the Greek mu
    [InlineData('\u03BC')] // Greek mu
    [InlineData('\u01C4')] // DZ with caron, which has a title case
    [InlineData('\u01C5')] // Dz with caron
    [InlineData('\u01C6')] // dz with caron
    [InlineData('\u0345')] // The combining Greek iota, one of four
    [InlineData('\u0130')] // Dotted capital I
    [InlineData('\u0131')] // Dotless i
    [InlineData('\u017F')] // Long s
    [InlineData('\u212A')] // The Kelvin sign
    [InlineData('\u00DF')] // Sharp s
    [InlineData('\u4E2D')] // A CJK ideograph
    public void ShouldExpectTheCharsTheComparisonTakes(char first)
    {
        var expected = OrdinalIgnoreCaseChars(first);

        foreach (var culture in new[] { "", "en-US", "tr-TR" })
        {
            using (new Culture(culture))
            {
                var seekable = (ISeekable)new TextLiteral(first + "x", StringComparison.OrdinalIgnoreCase);

                Assert.True(seekable.CanSeek);

                var chars = (char[])seekable.ExpectedChars.Clone();
                Array.Sort(chars);

                Assert.Equal(expected, chars);
            }
        }
    }

    [Fact]
    public void ShouldExpectTheFirstHalfOfACharBeyondTheBmp()
    {
        // A Deseret letter and its other case
        var lower = char.ConvertFromUtf32(0x10428);
        var upper = char.ConvertFromUtf32(0x10400);

        var literal = Literals.Text(lower, caseInsensitive: true);

        Assert.Equal([lower[0]], ((ISeekable)literal).ExpectedChars);

        AssertSameAsWithoutLookup(literal, lower);
        AssertSameAsWithoutLookup(literal, upper);
    }

    [Theory]
    [InlineData("\u03C3x", "\u03C3x")] // sigma
    [InlineData("\u03C3x", "\u03C2x")] // sigma and final sigma
    [InlineData("\u03C3x", "\u03A3x")] // sigma and capital sigma
    [InlineData("\u03C2x", "\u03C3x")] // final sigma and sigma
    [InlineData("\u00B5s", "\u03BCs")] // micro sign and mu
    [InlineData("\u03BCs", "\u00B5s")] // mu and micro sign
    [InlineData("\u01C5x", "\u01C5x")] // Dz, which is neither its upper nor its lower case
    [InlineData("\u01C5x", "\u01C4x")] // Dz and DZ
    [InlineData("\u01C4x", "\u01C5x")] // DZ and Dz
    [InlineData("\u00E9x", "\u00C9x")] // e acute and E acute
    [InlineData("\u00E9x", "ex")] // e acute and e
    [InlineData("ix", "\u0131x")] // i and dotless i
    [InlineData("ix", "\u0130x")] // i and dotted capital I
    [InlineData("sx", "\u017Fx")] // s and long s
    [InlineData("kx", "\u212Ax")] // k and the Kelvin sign
    public void ShouldMatchWhatTheTextMatchesOnItsOwn(string text, string input)
    {
        foreach (var culture in new[] { "", "tr-TR" })
        {
            using (new Culture(culture))
            {
                AssertSameAsWithoutLookup(Literals.Text(text, caseInsensitive: true), input);
            }
        }
    }

    [Theory]
    [InlineData("id")]
    [InlineData("ID")]
    [InlineData("\u0130D")] // Dotted capital I, the upper case of 'i' to a Turkish culture
    [InlineData("\u0131d")] // Dotless i, the lower case of 'I' to a Turkish culture
    [InlineData("xd")]
    public void ShouldCompareWithTheCultureCurrentWhenParsing(string input)
    {
        Parser<string> literal;

        using (new Culture("en-US"))
        {
            literal = new TextLiteral("id", StringComparison.CurrentCultureIgnoreCase);
        }

        // What the text starts with depends on a culture that isn't known when it is built
        Assert.False(((ISeekable)literal).CanSeek);

        foreach (var culture in new[] { "tr-TR", "en-US", "" })
        {
            using (new Culture(culture))
            {
                AssertSameAsWithoutLookup(literal, input);
            }
        }
    }

    // A choice only tries the alternatives which can start with the next char, and has to find the text
    // whenever the text matches when it is tried alone
    private static void AssertSameAsWithoutLookup(Parser<string> literal, string input)
    {
        var expected = literal.TryParse(input, out _);

        Assert.Equal(expected, literal.Or(Literals.Text("!")).TryParse(input, out _));
        Assert.Equal(expected, NotSeekable(literal).Or(Literals.Text("!")).TryParse(input, out _));
        Assert.Equal(expected, AnyCharBefore(literal).And(literal).TryParse("  " + input, out _));
    }
}
