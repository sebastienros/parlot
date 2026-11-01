#nullable enable

using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Generated pooled strings must match the runtime parser, and return the same string for the same value, from strings
// and from readers.
public class PooledStringTests
{
    private delegate bool StringParser(string text, out IReadOnlyList<string> value);
    private delegate bool ReaderParser(TextReader reader, out IReadOnlyList<string> value);

    private static void Check(Parser<IReadOnlyList<string>> runtime, StringParser generated, ReaderParser reader, string input)
    {
        var expected = runtime.TryParse(input, out var expectedValue);

        Assert.Equal(expected, generated(input, out var value));
        Assert.Equal(expected, reader(new StringReader(input), out var readValue));

        if (expected)
        {
            Assert.Equal(expectedValue, value);
            Assert.Equal(expectedValue, readValue);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("foo bar foo")]
    [InlineData("abcdefgh_X_ijklmnop abcdefgh_Y_ijklmnop abcdefgh_X_ijklmnop")]
    [InlineData("foo 1")]
    public void Identifiers_Match_The_Runtime_Parser(string input)
    {
        Check(ZeroOrMany(Terms.Identifier().ToPooledString()).Eof(), PooledStringGrammars.TryParseIdentifiers, PooledStringGrammars.TryParseIdentifiers, input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("'a'")]
    [InlineData("'a\\nb' \"a\\nb\" 'plain' \"plain\"")]
    [InlineData("'a\\u0041' 'unterminated")]
    public void Strings_Match_The_Runtime_Parser(string input)
    {
        Check(ZeroOrMany(Terms.String().ToPooledString()).Eof(), PooledStringGrammars.TryParseStrings, PooledStringGrammars.TryParseStrings, input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("$a;$b;$a;")]
    [InlineData("$a;$")]
    public void Span_Code_Matches_The_Runtime_Parser(string input)
    {
        Check(ZeroOrMany(Capture(Literals.Char('$').And(Literals.Identifier())).ToPooledString().AndSkip(Literals.Char(';'))).Eof(),
            PooledStringGrammars.TryParseSpans, PooledStringGrammars.TryParseSpans, input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SELECT select Select FROM from From SELECT FROM")]
    [InlineData("selects")]
    [InlineData("fromage")]
    public void Matched_Texts_Match_The_Runtime_Parser(string input)
    {
        Check(ZeroOrMany(OneOf(
                Terms.Text("select", caseInsensitive: true, returnMatchedText: true),
                Terms.Keyword("from", caseInsensitive: true, returnMatchedText: true))).Eof(),
            PooledStringGrammars.TryParseMatchedTexts, PooledStringGrammars.TryParseMatchedTexts, input);
    }

    [Fact]
    public void Repeated_Values_Are_The_Same_String()
    {
        Assert.True(PooledStringGrammars.TryParseIdentifiers("foo bar foo", out var identifiers));
        Assert.Same(identifiers[0], identifiers[2]);

        Assert.True(PooledStringGrammars.TryParseIdentifiers(" foo", out var again));
        Assert.Same(identifiers[0], again[0]);

        Assert.True(PooledStringGrammars.TryParseStrings("'a\\nb' \"a\\nb\" 'x' 'x'", out var strings));
        Assert.Equal(["a\nb", "a\nb", "x", "x"], strings);
        Assert.Same(strings[0], strings[1]);

        Assert.True(PooledStringGrammars.TryParseSpans("$abc;$abc;", out var spans));
        Assert.Equal(["$abc", "$abc"], spans);
        Assert.Same(spans[0], spans[1]);

        Assert.True(PooledStringGrammars.TryParseMatchedTexts("SELECT FROM select from SELECT FROM", out var texts));
        Assert.Same("select", texts[2]);
        Assert.Same("from", texts[3]);
        Assert.Same(texts[0], texts[4]);
        Assert.Same(texts[1], texts[5]);
    }

    [Fact]
    public void Reader_Entry_Points_Pool_Values()
    {
        Assert.True(PooledStringGrammars.TryParseIdentifiers(new StringReader("foo bar foo"), out var identifiers));
        Assert.Equal(["foo", "bar", "foo"], identifiers);
        Assert.Same(identifiers[0], identifiers[2]);

        Assert.True(PooledStringGrammars.TryParseMatchedTexts(new StringReader("Select Select"), out var texts));
        Assert.Same(texts[0], texts[1]);
    }
}

public static partial class PooledStringGrammars
{
    public static partial bool TryParseIdentifiers(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseIdentifiers(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseStrings(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseStrings(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseSpans(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSpans(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseMatchedTexts(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseMatchedTexts(TextReader reader, out IReadOnlyList<string> value);
}
