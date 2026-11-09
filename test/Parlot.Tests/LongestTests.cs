#nullable enable

using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class LongestTests
{
    private static readonly Parser<object> Integer = Terms.Integer().Then<object>(static x => x);
    private static readonly Parser<object> Decimal = Terms.Decimal().Then<object>(static x => x);

    [Theory]
    [InlineData("12", typeof(long))]
    [InlineData("12.5", typeof(decimal))]
    [InlineData(" 7", typeof(long))]
    [InlineData(" 7.0", typeof(decimal))]
    public void ShouldKeepTheLongestMatch(string text, Type type)
    {
        // OneOf(integer, dec) fails on 12.5, OneOf(dec, integer) returns 12 as a decimal
        Assert.True(Longest(Integer, Decimal).Eof().TryParse(text, out var value));
        Assert.IsType(type, value);
    }

    [Fact]
    public void ShouldKeepTheFirstOfTheMatchesOfTheSameLength()
    {
        Assert.IsType<long>(Longest(Integer, Decimal).Parse("12"));
        Assert.IsType<decimal>(Longest(Decimal, Integer).Parse("12"));

        var parser = Longest(Literals.Text("ab").Then(static _ => 1), Literals.Text("ab").Then(static _ => 2), Literals.Text("a").Then(static _ => 3));
        Assert.Equal(1, parser.Parse("abc"));
    }

    [Theory]
    [InlineData("end:", "label end")]
    [InlineData("a := 0", "expression a")]
    [InlineData("total", "expression total")]
    public void ShouldChooseByTheInput(string text, string expected)
    {
        var expressionStatement = Terms.Identifier().And(ZeroOrOne(Terms.Text(":=").SkipAnd(Terms.Integer())))
            .Then(static x => $"expression {x.Item1}");
        var label = Terms.Identifier().AndSkip(Terms.Char(':')).Then(static x => $"label {x}");

        // OneOf fails on one of the inputs, whatever the order of the alternatives
        Assert.Equal(expected, Longest(expressionStatement, label).Eof().Parse(text));
        Assert.Equal(expected, Longest(label, expressionStatement).Eof().Parse(text));
    }

    [Fact]
    public void ShouldContinueAfterTheLongestMatch()
    {
        var parser = Longest(Literals.Text("a"), Literals.Text("ab"), Literals.Text("abc")).And(Literals.Text("d"));

        Assert.Equal(("abc", "d"), parser.Parse("abcd"));
        Assert.Equal(("ab", "d"), parser.Parse("abd"));
        Assert.Equal(("a", "d"), parser.Parse("ad"));
    }

    [Fact]
    public void ShouldNotTryAShorterMatchWhenWhatFollowsFails()
    {
        // The parse continues after the longest match only, like the maximal munch of a lexer
        var parser = Longest(Literals.Text("ab"), Literals.Text("a")).And(Literals.Text("bc"));

        Assert.False(parser.TryParse("abc", out _));
        Assert.True(parser.TryParse("abbc", out var value));
        Assert.Equal(("ab", "bc"), value);
    }

    [Fact]
    public void ShouldMoveTheCursorToTheEndOfTheLongestMatch()
    {
        var parser = Longest(Literals.Text("abc"), Literals.Text("a"), Literals.Text("ab"));
        var context = new ParseContext(new Scanner("abcd"));
        var result = new ParseResult<string>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("abc", result.Value);
        Assert.Equal(0, result.Start);
        Assert.Equal(3, result.End);
        Assert.Equal(3, context.Scanner.Cursor.Offset);
        Assert.Equal('d', context.Scanner.Cursor.Current);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("  x")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1.")]
    public void ShouldRestoreTheCursorWhenNothingMatches(string text)
    {
        AssertFailsAtStart(Longest(Integer, Decimal).AndSkip(Literals.Char(';')), text);
        AssertFailsAtStart(Longest(Terms.Text("ab"), Terms.Text("abc")), text);
        AssertFailsAtStart(Longest(Terms.Text("ab"), Literals.Text("abc")), text);
        AssertFailsAtStart(Longest(Literals.Pattern(char.IsUpper), Literals.Text("abc").Then(static x => new TextSpan(x))), text);
        AssertFailsAtStart(Longest<string>(), text);
        AssertFailsAtStart(Longest(static matches => matches.IndexOfLongest(), Terms.Text("ab"), Terms.Text("abc")), text);
    }

    [Fact]
    public void ShouldSkipTheWhiteSpaceOnceWhenEveryAlternativeSkipsIt()
    {
        var parser = Longest(Terms.Text("ab"), Terms.Text("abc"), Terms.Text("b"));

        Assert.True(((ISeekable)parser).SkipWhitespace);

        var context = new ParseContext(new Scanner("   abcd"));
        var result = new ParseResult<string>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("abc", result.Value);
        Assert.Equal(3, result.Start);
        Assert.Equal(6, result.End);
        Assert.Equal(6, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldMatchAlternativesWhichSkipWhiteSpaceAndOthersWhichDont()
    {
        var parser = Longest(Terms.Text("ab"), Literals.Text(" a"), Literals.Text(" abc"));

        Assert.False(((ISeekable)parser).SkipWhitespace);
        Assert.Equal(" abc", parser.Parse(" abc"));
        Assert.Equal("ab", parser.Parse(" ab"));
        Assert.Equal("ab", parser.Parse("  ab"));
        Assert.Null(parser.Parse("  x"));
    }

    [Fact]
    public void ShouldOnlyInvokeTheAlternativesWhichCanStartWithTheNextChar()
    {
        var a = Literals.Text("a");
        var ab = Literals.Text("ab");
        var b = Literals.Text("b");
        var digits = Literals.Pattern(char.IsDigit).Then(static x => x.ToString());
        var parser = Longest(a, ab, b, digits);

        Assert.Equal([a, ab, digits], Invoked(parser, "ab", a, ab, b, digits));
        Assert.Equal([b, digits], Invoked(parser, "b", a, ab, b, digits));

        // The pattern can't tell what it starts with
        Assert.Equal([digits], Invoked(parser, "1", a, ab, b, digits));
        Assert.Equal([digits], Invoked(parser, "", a, ab, b, digits));

        // An alternative which tells it can start with any char above ASCII
        var identifier = Literals.Scan(ScanWord, WordStart.AsSpan()).Then(static x => x.ToString());
        var choice = Longest(a, ab, identifier);
        Assert.Equal([a, ab, identifier], Invoked(choice, "abc", a, ab, identifier));
        Assert.Equal([identifier], Invoked(choice, "été", a, ab, identifier));
        Assert.Empty(Invoked(choice, "1", a, ab, identifier));
        Assert.Equal("ab", choice.Parse("ab"));
        Assert.Equal("abc", choice.Parse("abc"));
        Assert.Equal("été", choice.Parse("été"));
    }

    [Fact]
    public void ShouldNotFlattenTheChoicesAmongItsAlternatives()
    {
        // The nested choice stops at its first match, the shorter one, which then competes with the third alternative
        var parser = Longest(OneOf(Literals.Text("a"), Literals.Text("abc")), Literals.Text("ab"));

        Assert.Equal("ab", parser.Parse("abc"));
    }

    [Fact]
    public void ShouldTellTheCharsOfItsAlternatives()
    {
        var parser = (ISeekable)Longest(Terms.Text("ab"), Terms.Text("cd"), Terms.Char('e').Then(static x => x.ToString()));

        Assert.True(parser.CanSeek);
        Assert.True(parser.SkipWhitespace);
        Assert.Equal(['a', 'c', 'e'], parser.ExpectedChars.OrderBy(static c => c));

        // An alternative which can't tell what it starts with
        Assert.False(((ISeekable)Longest(Terms.Text("ab"), Terms.Pattern(char.IsDigit).Then(static x => x.ToString()))).CanSeek);

        // The alternatives skip white space differently
        Assert.False(((ISeekable)Longest(Terms.Text("ab"), Literals.Text("cd"))).CanSeek);
    }

    [Fact]
    public void ShouldBeSelectedByAChoiceHoldingIt()
    {
        var a = Literals.Text("a");
        var ab = Literals.Text("ab");
        var longest = Longest(a, ab);
        var other = Literals.Text("x");
        var parser = OneOf(longest, other);

        Assert.Equal("ab", parser.Parse("ab"));
        Assert.Equal("x", parser.Parse("x"));
        Assert.Equal([longest, a, ab], Invoked(parser, "ab", longest, a, ab, other));
        Assert.Equal([other], Invoked(parser, "x", longest, a, ab, other));
    }

    [Fact]
    public void ShouldReadTheHintsOfDeferredAlternativesSetLater()
    {
        var number = Deferred<object>();
        var word = Deferred<object>();
        var parser = Longest(number, word);
        var choice = OneOf(parser, Terms.Char(';').Then<object>(static x => x));

        number.Parser = Integer.Or(Decimal);
        word.Parser = Terms.Text("12a").Then<object>(static x => x);

        Assert.True(((ISeekable)parser).CanSeek);
        Assert.Equal("12a", choice.Parse("12a"));
        Assert.Equal(12L, choice.Parse("12b"));
        Assert.Equal(';', choice.Parse(";"));
        Assert.Empty(Invoked(choice, ";", parser));
    }

    [Fact]
    public void ShouldMatchEmptyAlternatives()
    {
        var parser = Longest(Literals.Text("a").Else("empty"), Literals.Text("ab"));

        Assert.Equal("ab", parser.Parse("ab"));
        Assert.Equal("a", parser.Parse("ac"));
        Assert.Equal("empty", parser.Parse("x"));
        Assert.Equal("empty", parser.Parse(""));
    }

    [Fact]
    public void ShouldSelectTheMatch()
    {
        var keyword = Terms.Keyword("class").Then(static x => "keyword");
        var identifier = Terms.Identifier().Then(static x => "identifier " + x);

        // Keywords win over the identifiers of the same length, like tree-sitter's keyword extraction
        var parser = Longest(static matches =>
        {
            var best = matches.IndexOfLongest();

            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i].End == matches[best].End && matches[i].Alternative == 1)
                {
                    return i;
                }
            }

            return best;
        }, identifier, keyword);

        Assert.Equal("keyword", parser.Parse("class"));
        Assert.Equal("identifier classes", parser.Parse("classes"));
        Assert.Equal("identifier name", parser.Parse(" name"));
    }

    [Fact]
    public void ShouldPassTheMatchesInTheOrderOfTheAlternatives()
    {
        var seen = new List<(int Alternative, string Value, int Start, int End)>();
        var parser = Longest(matches =>
        {
            seen.Clear();

            for (var i = 0; i < matches.Count; i++)
            {
                seen.Add((matches[i].Alternative, matches[i].Value, matches[i].Start, matches[i].End));
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => matches[matches.Count]);
            return 0;
        }, Terms.Text("a"), Terms.Text("b"), Terms.Text("ab"), Terms.Pattern(char.IsLetter).Then(static x => x.ToString()));

        var context = new ParseContext(new Scanner("  abc"));
        var result = new ParseResult<string>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal([(0, "a", 2, 3), (2, "ab", 2, 4), (3, "abc", 2, 5)], seen);

        // The first match is kept, the cursor is after it
        Assert.Equal("a", result.Value);
        Assert.Equal(2, result.Start);
        Assert.Equal(3, result.End);
        Assert.Equal(3, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void ShouldFailWhenTheSelectorRejectsTheMatches(int index)
    {
        var invoked = 0;
        var parser = Longest((ParseContext context, LongestMatches<string> matches) =>
        {
            invoked++;
            Assert.Equal(3, matches.Count);
            Assert.NotNull(context);
            return index;
        }, Literals.Text("abc"), Literals.Text("ab"), Literals.Text("a"));

        // Parsed twice, with and without collecting the diagnostics
        AssertFailsAtStart(parser, "abc");
        Assert.Equal(2, invoked);

        // The selector isn't invoked when nothing matches
        AssertFailsAtStart(parser, "x");
        Assert.Equal(2, invoked);

        // The next alternative reads the same text
        Assert.Equal("abc!", parser.Or(Literals.Text("abc!")).Parse("abc!"));
    }

    [Fact]
    public void ShouldReportRejectedMatchesWhereTheyStart()
    {
        // Like a value When rejects, rather than where the longest match ends
        var parser = Longest(static _ => -1, Terms.Text("ab"), Terms.Text("a"));
        var context = new ParseContext(new Scanner("  ab"));

        Assert.False(parser.TryParse(context, out _, out _));
        Assert.Equal(2, context.Scanner.Cursor.FurthestOffset);

        // A shorter match is kept, the parse fails after it
        var shorter = Longest(static matches => matches.Count - 1, Literals.Text("abc"), Literals.Text("a")).AndSkip(Literals.Char('!'));
        context = new ParseContext(new Scanner("abc"));

        Assert.False(shorter.TryParse(context, out _, out _));
        Assert.Equal(1, context.Scanner.Cursor.FurthestOffset);
    }

    [Fact]
    public void ShouldKeepTheDiagnosticsOfTheMatchItKeeps()
    {
        var parser = Longest(
            Literals.Text("ab").Warning("short"),
            Literals.Text("abc").Warning("long"),
            Literals.Text("abcd").ElseError("unused"),
            Literals.Text("x"));

        Assert.Equal(["long"], Diagnostics(parser, "abc"));
        Assert.Equal(["short"], Diagnostics(parser, "ab"));
        Assert.Empty(Diagnostics(parser, "x"));

        var selecting = Longest(static _ => 0,
            Literals.Text("ab").Warning("short"),
            Literals.Text("abc").Warning("long"));

        Assert.Equal(["short"], Diagnostics(selecting, "abc"));
    }

    [Fact]
    public void ShouldKeepTheErrorsOfTheFurthestFailureWhenNothingMatches()
    {
        var parser = Longest(
            Literals.Text("a").SkipAnd(Literals.Text("b").ElseError("expected b")),
            Literals.Text("ac").SkipAnd(Literals.Text("d").ElseError("expected d")));

        var context = new ParseContext(new Scanner("acx")) { CollectDiagnostics = true };

        Assert.False(parser.TryParse(context, out _, out _));
        Assert.Equal(["expected d"], context.Diagnostics.Select(static x => x.Message));
    }

    [Fact]
    public void ShouldReportTheFurthestFailure()
    {
        var parser = Longest(Literals.Text("ab"), Literals.Text("abc").AndSkip(Literals.Char(';')).Then(static x => x)).AndSkip(Literals.Char('!'));

        var context = new ParseContext(new Scanner("abc;x"));

        Assert.False(parser.TryParse(context, out _, out _));
        Assert.Equal(4, context.Scanner.Cursor.FurthestOffset);
    }

    [Fact]
    public void ShouldDetectLeftRecursion()
    {
        var parser = Recursive<string>(static self => Longest(self.AndSkip(Literals.Char('+')), Literals.Text("a")));

        // The left-recursive alternative fails where it recurses, the other one matches
        Assert.Equal("a", parser.Parse("a+"));
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData("abc")]
    [InlineData("ab")]
    [InlineData("x")]
    public void ShouldNotAllocate(string text)
    {
        var choice = Longest(Literals.Text("a"), Literals.Text("ab"), Literals.Text("abc"), Literals.Text("x"));
        Assert.Equal(0, CaptureTests.MeasureAllocations(choice, text));

        var selecting = Longest(static matches => matches.Count - 1, Literals.Text("a"), Literals.Text("ab"), Literals.Text("abc"), Literals.Text("x"));
        Assert.Equal(0, CaptureTests.MeasureAllocations(selecting, text));
    }
#endif

    [Fact]
    public void ShouldParseTheTokensOfALexer()
    {
        // Maximal munch: the operators and the identifiers are the longest tokens at each position
        var token = Longest(
            Terms.Text("<"), Terms.Text("<="), Terms.Text("<<"), Terms.Text("<<="),
            Terms.Keyword("in"), Terms.Identifier().Then(static x => "id:" + x),
            Terms.Integer().Then(static x => "int:" + x.ToString(CultureInfo.InvariantCulture)),
            Terms.Decimal().Then(static x => "dec:" + x.ToString(CultureInfo.InvariantCulture)));

        var tokens = ZeroOrMany(token).Eof().Then(static x => string.Join(" ", x));

        Assert.Equal("in id:int id:i <<= int:1 <= int:12 dec:1.5 < <<", tokens.Parse("in int i <<=1<= 12 1.5 < <<"));
    }

    // Lowercase ASCII letters and any char above ASCII
    private static readonly string WordStart = "abcdefghijklmnopqrstuvwxyz" + new string(Enumerable.Range(0x80, 0x200).Select(static i => (char)i).ToArray());

    private static int ScanWord(ReadOnlySpan<char> span)
    {
        var length = 0;

        while (length < span.Length && WordStart.IndexOf(span[length]) >= 0)
        {
            length++;
        }

        return length;
    }

    private static List<object> Invoked<T>(Parser<T> parser, string text, params object[] parsers)
    {
        var invoked = new List<object>();
        var context = new ParseContext(new Scanner(text))
        {
            OnEnterParser = (p, _) =>
            {
                if (parsers.Contains(p))
                {
                    invoked.Add(p);
                }
            },
        };

        parser.TryParse(context, out _, out _);
        return invoked;
    }

    private static List<string> Diagnostics<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text)) { CollectDiagnostics = true };

        Assert.True(parser.TryParse(context, out _, out _));
        return context.Diagnostics.Select(static x => x.Message).ToList();
    }

    private static void AssertFailsAtStart<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // Also when the diagnostics are collected, which keeps the matches
        context = new ParseContext(new Scanner(text)) { CollectDiagnostics = true };

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }
}
