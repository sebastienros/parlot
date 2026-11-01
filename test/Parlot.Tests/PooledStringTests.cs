using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class PooledStringTests
{
    private static IReadOnlyList<string> ParseAll(Parser<string> value, string input)
    {
        Assert.True(ZeroOrMany(value).Eof().TryParse(input, out var values));
        return values;
    }

    [Fact]
    public void RepeatedValuesAreTheSameString()
    {
        var values = ParseAll(Terms.Identifier().ToPooledString(), "foo bar foo bar foo");

        Assert.Equal(["foo", "bar", "foo", "bar", "foo"], values);
        Assert.Same(values[0], values[2]);
        Assert.Same(values[0], values[4]);
        Assert.Same(values[1], values[3]);
        Assert.NotSame(values[0], values[1]);
    }

    [Fact]
    public void ValuesArePooledAcrossParses()
    {
        var parser = Terms.Identifier().ToPooledString().AndSkip(Terms.Char(';'));

        Assert.True(parser.TryParse("value;", out var first));
        Assert.True(parser.TryParse("  value ;", out var second));

        Assert.Equal("value", first);
        Assert.Same(first, second);
    }

    [Fact]
    public void ParsersHaveTheirOwnPool()
    {
        var parser = Terms.Identifier().ToPooledString().AndSkip(Terms.Char(';'));
        var other = Terms.Identifier().ToPooledString().AndSkip(Terms.Char(';'));

        Assert.True(parser.TryParse("value;", out var first));
        Assert.True(other.TryParse("value;", out var second));

        Assert.Equal(first, second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void WholeInputIsReturnedAsIs()
    {
        var input = string.Concat("who", "le");

        Assert.True(Literals.Identifier().ToPooledString().TryParse(input, out var value));
        Assert.Same(input, value);
    }

    [Fact]
    public void SingleCharsAndEmptyValues()
    {
        var values = ParseAll(Terms.NonWhiteSpace().ToPooledString(), "a b a \u00e9 \u00e9");

        Assert.Equal(["a", "b", "a", "\u00e9", "\u00e9"], values);
        Assert.Same(values[0], values[2]);
        Assert.Same(values[3], values[4]);

        Assert.True(Literals.Pattern(char.IsDigit, minSize: 0).ToPooledString().AndSkip(Literals.Char('x')).TryParse("x", out var empty));
        Assert.Same("", empty);
    }

    [Fact]
    public void ShouldRestoreCursorOnFailure()
    {
        var parser = Terms.Identifier().ToPooledString();
        var context = new ParseContext(new Scanner("  123"));
        var result = new ParseResult<string>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Theory]
    // Up to 16 chars the first and last 8 are the key
    [InlineData("abcdefgh", "abcdefgi")]
    [InlineData("abcdefghi", "abcdefghj")]
    [InlineData("abcdefghXjklmnop", "abcdefghYjklmnop")]
    [InlineData("abc", "abd")]
    [InlineData("abc", "abcd")]
    // Longer values also compare their middle chars
    [InlineData("abcdefgh_X_ijklmnop", "abcdefgh_Y_ijklmnop")]
    [InlineData("abcdefgh_1234567890_X_1234567890_ijklmnop", "abcdefgh_1234567890_Y_1234567890_ijklmnop")]
    public void ValuesWhichShareTheirEndsAreDistinct(string a, string b)
    {
        var values = ParseAll(Terms.Identifier().ToPooledString(), $"{a} {b} {a} {b}");

        Assert.Equal([a, b, a, b], values);
        Assert.Same(values[0], values[2]);
        Assert.Same(values[1], values[3]);
    }

    [Fact]
    public void LongValuesAreNotPooled()
    {
        var value = new string('a', 65);
        var values = ParseAll(Terms.Identifier().ToPooledString(), $"{value} {value}");

        Assert.Equal([value, value], values);
        Assert.NotSame(values[0], values[1]);

        var pooled = new string('a', 64);
        values = ParseAll(Terms.Identifier().ToPooledString(), $"{pooled} {pooled}");

        Assert.Same(values[0], values[1]);
    }

    [Fact]
    public void ManyDistinctValuesAreCorrect()
    {
        // More values than the pool holds, and values that rarely repeat, which switch it off
        var parser = Terms.Identifier().ToPooledString();
        var names = Enumerable.Range(0, 10_000).Select(static i => "id" + i).ToArray();

        var values = ParseAll(parser, string.Join(" ", names));
        Assert.Equal(names, values);

        values = ParseAll(parser, string.Join(" ", Enumerable.Reverse(names)));
        Assert.Equal(Enumerable.Reverse(names), values);
    }

    [Fact]
    public void FullPoolKeepsItsValues()
    {
        var parser = Terms.Identifier().ToPooledString();

        // As many values as the pool holds, which repeat, so that it stays on
        var names = Enumerable.Range(0, 4096).Select(static i => "name" + i).ToArray();
        var input = string.Join(" ", names.SelectMany(static name => new[] { name, name, name }));

        var values = ParseAll(parser, input);
        Assert.Equal(names.SelectMany(static name => new[] { name, name, name }), values);
        Assert.Same(values[0], values[1]);

        // The full pool keeps finding its values, so it keeps them
        var again = ParseAll(parser, input);

        for (var i = 0; i < values.Count; i++)
        {
            Assert.Same(values[i], again[i]);
        }

        // New values are created, the pooled ones are still found
        var others = Enumerable.Range(0, 2_000).Select(static i => "other" + i).ToArray();
        Assert.Equal(others, ParseAll(parser, string.Join(" ", others)));
        Assert.Same(values[0], ParseAll(parser, " " + names[0])[0]);
        Assert.Same(values[3], ParseAll(parser, " " + names[1])[0]);
    }

    [Fact]
    public void FullPoolOfValuesThatDontRepeatSwitchesOff()
    {
        var parser = Terms.Identifier().ToPooledString();

        // Values which repeat a few times, then more values than the pool holds, which keep missing it
        var names = Enumerable.Range(0, 6_000).Select(static i => "name" + i).ToArray();
        var input = string.Join(" ", names.SelectMany(static name => new[] { name, name, name }));

        Assert.Equal(names.SelectMany(static name => new[] { name, name, name }), ParseAll(parser, input));
        Assert.Equal(names.SelectMany(static name => new[] { name, name, name }), ParseAll(parser, input));
    }

    [Fact]
    public void ConcurrentParsesShareThePool()
    {
        var parser = ZeroOrMany(Terms.Identifier().ToPooledString()).Eof();
        var names = Enumerable.Range(0, 2_000).Select(static i => "name" + (i % 500)).ToArray();
        var input = string.Join(" ", names);

        Parallel.For(0, 64, _ =>
        {
            Assert.True(parser.TryParse(input, out var values));
            Assert.Equal(names, values);
        });

        Assert.True(parser.TryParse(input, out var first));
        Assert.True(parser.TryParse(input, out var second));

        for (var i = 0; i < first.Count; i++)
        {
            Assert.Same(first[i], second[i]);
        }
    }

    [Fact]
    public void StringLiteralsDecodeIntoThePool()
    {
        var values = ParseAll(Terms.String().ToPooledString(), "'a\\nb' \"a\\nb\" 'plain' \"plain\" 'a\\u0041'");

        Assert.Equal(["a\nb", "a\nb", "plain", "plain", "aA"], values);
        Assert.Same(values[0], values[1]);
        Assert.Same(values[2], values[3]);
    }

    [Fact]
    public void PooledStringLiteralsDontChangeTheirSource()
    {
        var literal = Terms.String();
        var pooled = literal.ToPooledString();

        Assert.True(pooled.TryParse("'a\\nb'", out var first));
        Assert.True(literal.TryParse("'a\\nb'", out var span1));
        Assert.True(literal.TryParse("'a\\nb'", out var span2));

        Assert.Equal("a\nb", first);
        Assert.Equal("a\nb", span1.ToString());

        // The literal decodes into a new string each time
        Assert.NotSame(span1.Buffer, span2.Buffer);
    }

    [Fact]
    public void PooledStringLiteralsKeepTheirName()
    {
        var literal = Literals.String().Named("text");

        Assert.Contains("text", literal.ToPooledString().ToString());
    }

    [Fact]
    public void PooledStringsAreSeekable()
    {
        var parser = ZeroOrMany(OneOf(
            Terms.Text("let"),
            Terms.Identifier().ToPooledString(),
            Terms.String().ToPooledString())).Eof();

        Assert.True(parser.TryParse("let a 'b' let", out var values));
        Assert.Equal(["let", "a", "b", "let"], values);
    }

    [Theory]
    [InlineData(StringComparison.OrdinalIgnoreCase)]
    [InlineData(StringComparison.InvariantCultureIgnoreCase)]
    public void CaseInsensitiveMatchedTextsArePooled(StringComparison comparison)
    {
        var text = new TextLiteral("select", comparison, returnMatchedText: true);
        var values = ParseAll(SkipWhiteSpace(text), "SELECT select Select SELECT select Select");

        Assert.Equal(["SELECT", "select", "Select", "SELECT", "select", "Select"], values);
        Assert.Same("select", values[1]);
        Assert.Same(values[0], values[3]);
        Assert.Same(values[2], values[5]);
    }

    [Fact]
    public void CaseInsensitiveMatchedKeywordsArePooled()
    {
        var values = ParseAll(Terms.Keyword("for", caseInsensitive: true, returnMatchedText: true), "FOR for For FOR For");

        Assert.Equal(["FOR", "for", "For", "FOR", "For"], values);
        Assert.Same("for", values[1]);
        Assert.Same(values[0], values[3]);
        Assert.Same(values[2], values[4]);
    }

    [Fact]
    public void CaseInsensitiveMatchedTextsArePooledAcrossParses()
    {
        var parser = Terms.Text("hello", caseInsensitive: true, returnMatchedText: true).AndSkip(Terms.Char('!'));

        Assert.True(parser.TryParse("HELLO!", out var first));
        Assert.True(parser.TryParse(" HELLO !", out var second));

        Assert.Equal("HELLO", first);
        Assert.Same(first, second);
    }

    [Fact]
    public void CaseInsensitiveMatchedTextsKeepTheirFirstCases()
    {
        var parser = Terms.Keyword("select", caseInsensitive: true, returnMatchedText: true);
        var cases = new[] { "SELECT", "Select", "sELECT", "SeLeCt", "sElEcT", "SELECt" };
        var input = string.Join(" ", cases.Concat(cases));
        var values = ParseAll(parser, input);

        Assert.Equal(cases.Concat(cases), values);

        // The first four cases are kept, the other ones are created each time
        for (var i = 0; i < 4; i++)
        {
            Assert.Same(values[i], values[i + cases.Length]);
        }

        for (var i = 4; i < cases.Length; i++)
        {
            Assert.NotSame(values[i], values[i + cases.Length]);
        }

        // The text itself is never kept
        Assert.Same("select", ParseAll(parser, "x select".Substring(1))[0]);
    }

    [Fact]
    public void ConcurrentParsesShareTheCases()
    {
        var parser = ZeroOrMany(Terms.Text("hello", caseInsensitive: true, returnMatchedText: true)).Eof();
        var cases = new[] { "HELLO", "Hello", "hELLO", "HeLLo", "hello", "HELLo" };
        var input = string.Join(" ", Enumerable.Range(0, 600).Select(i => cases[i % cases.Length]));
        var expected = Enumerable.Range(0, 600).Select(i => cases[i % cases.Length]).ToArray();

        Parallel.For(0, 64, _ =>
        {
            Assert.True(parser.TryParse(input, out var values));
            Assert.Equal(expected, values);
        });

        // Each kept case has a single string
        Assert.True(parser.TryParse(input, out var first));
        Assert.True(parser.TryParse(input, out var second));
        Assert.Equal(4, first.Zip(second).Where(static pair => !ReferenceEquals(pair.First, "hello") && ReferenceEquals(pair.First, pair.Second)).Select(static pair => pair.First).Distinct().Count());
    }
}
