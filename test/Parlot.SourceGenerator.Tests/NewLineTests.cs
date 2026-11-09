#nullable enable

using System;
using System.IO;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

// The generated NoNewLineBefore and NewLineAhead parsers match the runtime ones, from strings and from readers filled a few
// chars at a time.
public class NewLineTests
{
    private delegate bool StringParser<T>(string text, out T value);
    private delegate bool ReaderParser<T>(TextReader reader, out T value);

    public static readonly TheoryData<string> Inputs =
    [
        "", "a", "a b", "a;b", "a\nb", "a\r\nb", "a  \n  b", "a\n", "a  ", "a ;  ", "return", "return a", "return\na", "return\r\n a;",
        "a++", "a ++ b", "a\n++b", "a\n++", "a++\nb", "return a\n++b", "a /* x */ b", "a /* \n */ b", "a // c\n b", "return /* x */ a",
        "return /* \n */ a", "return // c\n a", "a++ /* c */\n b", "a /* b", "{", "a \u2028 b", "return \v a", "return \u0085 a",
    ];

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Statements(string input) => Check(RuntimeNewLineGrammars.Statements(), NewLineGrammars.TryParseStatements, NewLineGrammars.TryParseStatements, input);

    [Theory]
    [MemberData(nameof(Inputs))]
    public void StatementsWithComments(string input)
    {
        // An unterminated comment is an error, reported by the runtime and the generated parsers alike
        var parser = RuntimeNewLineGrammars.StatementsWithComments();
        var expected = Outcome(() => parser.TryParse(input, out var value) ? value! : "FAIL");

        Assert.Equal(expected, Outcome(() => NewLineGrammars.TryParseStatementsWithComments(input, out var value) ? value! : "FAIL"));

        foreach (var chunk in new[] { 1, 3, int.MaxValue })
        {
            Assert.Equal(expected, Outcome(() => NewLineGrammars.TryParseStatementsWithComments(new VectorizedTokenTests.ChunkedReader(input, chunk), out var value) ? value! : "FAIL"));
        }
    }

    private static string Outcome(Func<string> parse)
    {
        try
        {
            return parse();
        }
        catch (ParseException e)
        {
            return "ERROR " + e.Message;
        }
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void CustomNewLines(string input) => Check(RuntimeNewLineGrammars.CustomNewLines(), NewLineGrammars.TryParseCustomNewLines, NewLineGrammars.TryParseCustomNewLines, input);

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Captured(string input) => Check(RuntimeNewLineGrammars.Captured(), NewLineGrammars.TryParseCaptured, NewLineGrammars.TryParseCaptured, input);

    [Theory]
    [MemberData(nameof(Inputs))]
    public void AsciiSet(string input) => Check(RuntimeNewLineGrammars.AsciiSet(), NewLineGrammars.TryParseAsciiSet, NewLineGrammars.TryParseAsciiSet, input);

    [Theory]
    [MemberData(nameof(Inputs))]
    [InlineData("a ++ !\n b")]
    [InlineData("a /* \n */ ++b")]
    [InlineData("a // c\n++b ;")]
    public void PostfixTable(string input)
    {
        // An unterminated comment is an error, reported by the runtime and the generated parsers alike
        var parser = RuntimeNewLineGrammars.PostfixTable();
        var expected = Outcome(() => parser.TryParse(input, out var value) ? value! : "FAIL");

        Assert.Equal(expected, Outcome(() => NewLineGrammars.TryParsePostfixTable(input, out var value) ? value! : "FAIL"));

        foreach (var chunk in new[] { 1, 3, int.MaxValue })
        {
            Assert.Equal(expected, Outcome(() => NewLineGrammars.TryParsePostfixTable(new VectorizedTokenTests.ChunkedReader(input, chunk), out var value) ? value! : "FAIL"));
        }

        // The postfix operator doesn't apply after a new line
        if (input == "a\n++b")
        {
            Assert.Equal("a|++b", expected);
        }
    }

    [Fact]
    public void GeneratedParsersFindTheNewLines()
    {
        Assert.True(NewLineGrammars.TryParseStatements("return\na", out var value));
        Assert.Equal("return|a", value);
        Assert.True(NewLineGrammars.TryParseStatementsWithComments("return /* \n */ a\n++b", out value));
        Assert.Equal("return|a|++b", value);
        Assert.False(NewLineGrammars.TryParseStatements("a b", out _));

        Assert.True(NewLineGrammars.TryParseCustomNewLines("return \v a \u2028", out value));
        Assert.Equal("return a+nl", value);

        Assert.True(NewLineGrammars.TryParseAsciiSet("a \v b", out value));
        Assert.Equal("a", value);
    }

    private static void Check<T>(Parser<T> parser, StringParser<T> parseString, ReaderParser<T> parseReader, string input)
    {
        var expected = parser.TryParse(input, out var expectedValue);

        Assert.Equal(expected, parseString(input, out var value));
        Assert.Equal(expected ? expectedValue : default, value);

        foreach (var reader in new TextReader[] { new StringReader(input), new VectorizedTokenTests.ChunkedReader(input, 1), new VectorizedTokenTests.ChunkedReader(input, 3) })
        {
            Assert.Equal(expected, parseReader(reader, out value));
            Assert.Equal(expected ? expectedValue : default, value);
        }
    }
}

public static partial class NewLineGrammars
{
    public static partial bool TryParseStatements(string text, out string value);
    public static partial bool TryParseStatements(TextReader reader, out string value);
    public static partial bool TryParseStatementsWithComments(string text, out string value);
    public static partial bool TryParseStatementsWithComments(TextReader reader, out string value);
    public static partial bool TryParseCustomNewLines(string text, out string value);
    public static partial bool TryParseCustomNewLines(TextReader reader, out string value);
    public static partial bool TryParseCaptured(string text, out int value);
    public static partial bool TryParseCaptured(TextReader reader, out int value);
    public static partial bool TryParseAsciiSet(string text, out string value);
    public static partial bool TryParseAsciiSet(TextReader reader, out string value);
    public static partial bool TryParsePostfixTable(string text, out string value);
    public static partial bool TryParsePostfixTable(TextReader reader, out string value);
}

// The runtime parsers of NewLineGrammars, which the generator executes but the tests don't compile
public static class RuntimeNewLineGrammars
{
    // Statements ended by a semicolon, a '}', the end of the input or a new line, with "[no LineTerminator here]" before the
    // argument of return and before a postfix operator
    public static Parser<string> Statements()
    {
        var end = OneOf(Terms.Char(';').Then<object?>(static _ => null), Always().WhenFollowedBy(Terms.Char('}')), SkipWhiteSpace(Eof()), NewLineAhead());
        var returnStatement = Terms.Keyword("return").SkipAnd(Terms.Identifier().NoNewLineBefore().Then(static x => "return " + x.ToString()).Else("return"));
        var prefix = Terms.Text("++").SkipAnd(Terms.Identifier()).Then(static x => "++" + x.ToString());
        var expression = Terms.Identifier().And(NoNewLineBefore(Terms.Text("++")).Else("")).Then(static x => x.Item1.ToString() + x.Item2);
        return ZeroOrMany(OneOf(returnStatement, prefix, expression).AndSkip(end)).Eof().Then(static x => string.Join("|", x));
    }

    public static Parser<string> StatementsWithComments() =>
        Statements().WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));

    // JavaScript's line terminators, of which \v isn't one
    public static Parser<string> CustomNewLines() =>
        Terms.Keyword("return").SkipAnd(Terms.Identifier().NoNewLineBefore("\n\r\u2028\u2029").Then(static x => "return " + x.ToString()).Else("return"))
            .And(NewLineAhead("\n\r\u2028\u2029").Then(static _ => "+nl").Else(""))
            .Then(static x => x.Item1 + x.Item2);

    // A postfix '++' with "[no LineTerminator here]", a choice of the postfix operators of a table which skips the white space
    // once for its alternatives, with comments
    public static Parser<string> PostfixTable()
    {
        var end = OneOf(Terms.Char(';').Then<object?>(static _ => null), SkipWhiteSpace(Eof()), NewLineAhead());
        var expression = Terms.Identifier().Then(static x => x.ToString()).Operators()
            .Postfix(
                (NoNewLineBefore(Terms.Text("++")), static x => x + "++"),
                (Terms.Text("!"), static x => x + "!"));
        return ZeroOrMany(OneOf(Terms.Text("++").SkipAnd(Terms.Identifier()).Then(static x => "++" + x.ToString()), expression).AndSkip(end)).Eof()
            .Then(static x => string.Join("|", x))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));
    }

    // The values aren't used: the parsers are generated without them
    public static Parser<int> Captured() =>
        Capture(Terms.Identifier().And(Terms.Identifier().NoNewLineBefore()).And(NewLineAhead())).Then(static x => x.Length);

    // The new lines of the ASCII set: \v is one, U+2028 isn't white space
    public static Parser<string> AsciiSet() =>
        Terms.Identifier().And(Terms.Identifier().NoNewLineBefore().Then(static x => " " + x.ToString()).Else(""))
            .Then(static x => x.Item1.ToString() + x.Item2)
            .WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
}
