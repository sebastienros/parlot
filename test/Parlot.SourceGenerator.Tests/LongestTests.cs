#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// The generated Longest keeps the match the runtime one keeps, and leaves the cursor where it does.
/// </summary>
public class LongestTests
{
    private delegate bool StringEntry(string text, out IReadOnlyList<string> value);

    private delegate bool ReaderEntry(TextReader reader, out IReadOnlyList<string> value);

    [Theory]
    [InlineData("12")]
    [InlineData("12.5")]
    [InlineData(" 1 2.5 3 ")]
    [InlineData("1.5.5")]
    [InlineData("")]
    [InlineData("1.")]
    [InlineData("x")]
    public void Numbers_Keep_The_Longest_Match(string input)
    {
        Check(LongestGrammars.CreateNumbers(), LongestGrammars.TryParseNumbers, LongestGrammars.TryParseNumbers, input);
    }

    [Theory]
    [InlineData("end:")]
    [InlineData("a := 0")]
    [InlineData("end: total := 12 x")]
    [InlineData("a :=")]
    [InlineData("a:b:c:")]
    [InlineData(":")]
    public void Statements_Keep_The_Longest_Match(string input)
    {
        Check(LongestGrammars.CreateStatements(), LongestGrammars.TryParseStatements, LongestGrammars.TryParseStatements, input);
    }

    [Theory]
    [InlineData("in int i <<=1<= 12 1.5 < <<")]
    [InlineData("<<<<=")]
    [InlineData(" inx in")]
    [InlineData("1.x")]
    public void Tokens_Keep_The_Longest_Match(string input)
    {
        Check(LongestGrammars.CreateTokens(), LongestGrammars.TryParseTokens, LongestGrammars.TryParseTokens, input);
    }

    [Fact]
    public void Tokens_Are_The_Longest_Ones()
    {
        Assert.True(LongestGrammars.TryParseTokens("in int i <<=1<= 12 1.5 < <<", out var value));
        Assert.Equal("in id:int id:i <<= int:1 <= int:12 dec:1.5 < <<", string.Join(" ", value));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ab")]
    [InlineData("ax")]
    [InlineData("x")]
    [InlineData("é")]
    [InlineData("éa")]
    [InlineData("cd c cd")]
    [InlineData(" cd")]
    [InlineData("  c")]
    [InlineData("a b")]
    [InlineData("中")]
    public void Mixed_Alternatives_Keep_The_Longest_Match(string input)
    {
        Check(LongestGrammars.CreateMixed(), LongestGrammars.TryParseMixed, null, input);
    }

    [Theory]
    [InlineData("class classes cl")]
    [InlineData("klass class")]
    [InlineData("")]
    [InlineData("1")]
    public void Selector_Prefers_Keywords(string input)
    {
        Check(LongestGrammars.CreateKeywords(), LongestGrammars.TryParseKeywords, LongestGrammars.TryParseKeywords, input);
    }

    [Theory]
    [InlineData("abab")]
    [InlineData("ababab")]
    [InlineData("ab ab")]
    [InlineData("b")]
    [InlineData("abb")]
    [InlineData("")]
    public void Selector_Keeps_Or_Rejects_A_Match(string input)
    {
        Check(LongestGrammars.CreateShortest(), LongestGrammars.TryParseShortest, LongestGrammars.TryParseShortest, input);
    }

    [Theory]
    [InlineData("abb")]
    [InlineData("ab b")]
    [InlineData("abcb")]
    public void Rejected_Matches_Fail_Where_They_Start(string input)
    {
        var context = new ParseContext(new Scanner(input));

        Assert.False(LongestGrammars.CreateShortest().TryParse(context, out _, out _));
        Assert.False(LongestGrammars.TryParseShortest(input, out _, out int offset));
        Assert.Equal(context.Scanner.Cursor.FurthestOffset, offset);
    }

    [Theory]
    [InlineData("ab cx")]
    [InlineData("ab c ex")]
    [InlineData("abc")]
    [InlineData("ab")]
    [InlineData(" y")]
    [InlineData("")]
    public void Failures_Are_Reported_Where_The_Runtime_Reports_Them(string input)
    {
        // The span code of the alternatives, which fail at different depths
        var context = new ParseContext(new Scanner(input));

        Assert.False(LongestGrammars.CreateDepths().TryParse(context, out _, out _));
        Assert.False(LongestGrammars.TryParseDepths(input, out _, out int offset));
        Assert.Equal(context.Scanner.Cursor.FurthestOffset, offset);
    }

    [Theory]
    [InlineData("abcd", "abcd")]
    [InlineData(" ab c ef", "abcef")]
    [InlineData("x", "x")]
    public void Matches_Of_Different_Lengths_Keep_The_Longest(string input, string expected)
    {
        Assert.Equal(expected, LongestGrammars.CreateDepths().Parse(input));
        Assert.True(LongestGrammars.TryParseDepths(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("abc", "long")]
    [InlineData(" ab", "short")]
    [InlineData("abcd", "")]
    [InlineData("x", "")]
    public void Diagnostics_Are_The_Ones_Of_The_Kept_Match(string input, string expected)
    {
        Assert.Equal(expected, Diagnostics(LongestGrammars.CreateWarnings(), input));
        LongestGrammars.TryParseWarnings(input, out _, out var diagnostics);
        Assert.Equal(expected, string.Join(",", diagnostics.Select(static x => x.Message)));
    }

    [Fact]
    public void Diagnostics_Are_The_Ones_Of_The_Selected_Match()
    {
        Assert.Equal("short", Diagnostics(LongestGrammars.CreateSelectedWarnings(), "abc"));
        Assert.True(LongestGrammars.TryParseSelectedWarnings("abc", out var value, out var diagnostics));
        Assert.Equal("ab", value);
        Assert.Equal("short", string.Join(",", diagnostics.Select(static x => x.Message)));
    }

    [Theory]
    [InlineData("abc,a,ab")]
    [InlineData("abca")]
    [InlineData("x")]
    public void Discarded_Values_Keep_The_Longest_Match(string input)
    {
        Assert.Equal(LongestGrammars.CreateCaptured().TryParse(input, out var expected), LongestGrammars.TryParseCaptured(input, out var value));
        Assert.Equal(expected, value);
    }

    private static string Diagnostics<T>(Parser<T> parser, string input)
    {
        var context = new ParseContext(new Scanner(input)) { CollectDiagnostics = true };
        parser.TryParse(context, out _, out _);
        return string.Join(",", context.Diagnostics.Select(static x => x.Message));
    }

    private static void Check(Parser<IReadOnlyList<string>> runtime, StringEntry generated, ReaderEntry? reader, string input)
    {
        var success = runtime.TryParse(input, out var expected);

        Assert.Equal(success, generated(input, out var value));

        if (success)
        {
            Assert.Equal(string.Join("|", expected!), string.Join("|", value));
        }

        if (reader != null)
        {
            Assert.Equal(success, reader(new StringReader(input), out var read));

            if (success)
            {
                Assert.Equal(string.Join("|", expected!), string.Join("|", read));
            }
        }
    }
}

public static partial class LongestGrammars
{
    public static partial bool TryParseNumbers(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseNumbers(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseStatements(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseStatements(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseTokens(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseTokens(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseMixed(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseKeywords(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseKeywords(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseShortest(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseShortest(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseShortest(string text, out IReadOnlyList<string> value, out int offset);
    public static partial bool TryParseWarnings(string text, out string value,
        out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> diagnostics);
    public static partial bool TryParseSelectedWarnings(string text, out string value,
        out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> diagnostics);
    public static partial bool TryParseCaptured(string text, out string value);
    public static partial bool TryParseDepths(string text, out string value);
    public static partial bool TryParseDepths(string text, out string value, out int offset);

    // The runtime copies of the generated grammars

    public static Parser<IReadOnlyList<string>> CreateNumbers() => ZeroOrMany(Longest(
        Terms.Integer().Then(static x => "i" + x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Terms.Decimal().Then(static x => "d" + x.ToString(System.Globalization.CultureInfo.InvariantCulture)))).Eof();

    public static Parser<IReadOnlyList<string>> CreateStatements() => ZeroOrMany(Longest(
        Terms.Identifier().And(ZeroOrOne(Terms.Text(":=").SkipAnd(Terms.Integer()))).Then(static x => "e:" + x.Item1.ToString()),
        Terms.Identifier().AndSkip(Terms.Char(':')).Then(static x => "l:" + x.ToString()))).Eof();

    public static Parser<IReadOnlyList<string>> CreateTokens() => ZeroOrMany(Longest(
        Terms.Text("<"), Terms.Text("<="), Terms.Text("<<"), Terms.Text("<<="),
        Terms.Keyword("in"),
        Terms.Identifier().Then(static x => "id:" + x.ToString()),
        Terms.Integer().Then(static x => "int:" + x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Terms.Decimal().Then(static x => "dec:" + x.ToString(System.Globalization.CultureInfo.InvariantCulture)))).Eof();

    public static Parser<IReadOnlyList<string>> CreateMixed() => ZeroOrMany(OneOf(
        Longest(Literals.Text("ab"), Literals.Text("abc"), Literals.Pattern(static c => c == 'a' || c == 'x').Then(static x => "p:" + x.ToString()),
            Literals.Scan(static span => span.Length > 0 && span[0] >= '\u0080' ? 1 : 0, NonAsciiStart.AsSpan()).Then(static x => "u:" + x.ToString())),
        Longest(Terms.Text("cd"), Literals.Text(" c")),
        Literals.Char(' ').Then(static _ => "_"))).Eof();

    // More non-ASCII chars than a parser lists, see SeekableHints.MaxNonAsciiChars
    private const string NonAsciiStart = "éèêëàâäôöûüçîïÿñæœåøßþðáíóúýāēīōūăĕĭŏŭąęįųćčďěňřšťžźżłńśőűđħıĳľŀŉŋŧſ";

    public static Parser<IReadOnlyList<string>> CreateKeywords() => ZeroOrMany(Longest(static matches =>
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
    }, Terms.Identifier().Then(static x => "id:" + x.ToString()), Terms.Keyword("class").Then(static _ => "class"))).Eof();

    public static Parser<IReadOnlyList<string>> CreateShortest() => ZeroOrMany(Longest(static (ParseContext context, LongestMatches<string> matches) =>
        matches[0].Value.StartsWith("b", StringComparison.Ordinal) ? -1 : 0,
        Terms.Text("ab"), Terms.Text("abab"), Terms.Text("ababab"), Terms.Text("b"))).Eof();

    public static Parser<string> CreateWarnings() => Longest(
        Terms.Text("ab").Warning("short"),
        Terms.Text("abc").Warning("long"),
        Terms.Text("abcd").ElseError("unused"),
        Terms.Text("x"));

    public static Parser<string> CreateSelectedWarnings() => Longest(static matches => 0,
        Terms.Text("ab").Warning("short"),
        Terms.Text("abc").Warning("long"),
        Terms.Text("x"));

    public static Parser<string> CreateCaptured() => Capture(ZeroOrMany(Longest(Literals.Text("a"), Literals.Text("ab"), Literals.Text("abc"), Literals.Text(",")))).Then(static x => x.ToString());

    public static Parser<string> CreateDepths() => Longest(
        Terms.Text("ab").And(Terms.Text("cd")).Then(static x => x.Item1 + x.Item2),
        Terms.Text("ab").And(Terms.Text("c")).And(Terms.Text("ef")).Then(static x => x.Item1 + x.Item2 + x.Item3),
        Terms.Text("x"));
}
