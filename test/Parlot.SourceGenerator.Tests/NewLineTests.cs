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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StatementsWithCommentsCutByTheBuffer(bool terminated)
    {
        // The buffer of a generated reader starts at 4096 chars: a comment which it doesn't end is read again with more
        // text, and is only unterminated at the end of the input
        foreach (var length in new[] { 4090, 4094, 4095, 4096, 10_000 })
        {
            var input = "a;/*" + new string('x', length) + (terminated ? "*/ b\n/* " + new string('y', length) + " */ c" : "");
            var parser = RuntimeNewLineGrammars.StatementsWithComments();
            var expected = Outcome(() => parser.TryParse(input, out var value) ? value! : "FAIL");

            Assert.Equal(terminated ? "a|b|c" : "FAIL", expected);
            Assert.Equal(expected, Outcome(() => NewLineGrammars.TryParseStatementsWithComments(input, out var value) ? value! : "FAIL"));

            foreach (var chunk in new[] { 1, 3, int.MaxValue })
            {
                Assert.Equal(expected, Outcome(() => NewLineGrammars.TryParseStatementsWithComments(new VectorizedTokenTests.ChunkedReader(input, chunk), out var value) ? value! : "FAIL"));
            }
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

    // The white space before the arrow or the postfix operator crosses the end of the first buffer of a generated reader,
    // 4096 chars, with the new line before it or not
    public static readonly TheoryData<string> ChoiceInputs =
    [
        "a => b", "a=>b", "a\n=> b", "a\r\n=>b", "a = b", "a\n= b", "a += b", "a\n+= b", "a /* x */ => b", "a /* \n */ => b",
        "a // c\n=> b", "a\n/* x */ => b", "a /* b", "a++", "a ++ * b", "a\n++b", "a /* x */ --", "a /* \n */ ++b", "a // c\n++b",
        "a * b++ ++c", "a\n+ b", "a++\n++b", "a\n++",
        "a" + new string(' ', 4000) + "\n" + new string(' ', 200) + "=> b", "a\n" + new string(' ', 4200) + "=> b",
        "a" + new string(' ', 4200) + "=> b", "a" + new string(' ', 4200) + "= b", "a" + new string(' ', 4093) + "=> b",
        "a" + new string(' ', 4000) + "\n" + new string(' ', 200) + "++b", "a" + new string(' ', 4200) + "++",
    ];

    [Theory]
    [MemberData(nameof(ChoiceInputs))]
    public void ArrowChoice(string input) => CheckOutcome(RuntimeNewLineGrammars.ArrowChoice(), NewLineGrammars.TryParseArrowChoice, NewLineGrammars.TryParseArrowChoice, input);

    [Theory]
    [MemberData(nameof(ChoiceInputs))]
    public void ArrowChoiceWithComments(string input) => CheckOutcome(RuntimeNewLineGrammars.ArrowChoiceWithComments(), NewLineGrammars.TryParseArrowChoiceWithComments, NewLineGrammars.TryParseArrowChoiceWithComments, input);

    [Theory]
    [MemberData(nameof(ChoiceInputs))]
    public void PostfixTable(string input) => CheckOutcome(RuntimeNewLineGrammars.PostfixTable(), NewLineGrammars.TryParsePostfixTable, NewLineGrammars.TryParsePostfixTable, input);

    [Theory]
    [MemberData(nameof(ChoiceInputs))]
    public void PostfixTableWithComments(string input) => CheckOutcome(RuntimeNewLineGrammars.PostfixTableWithComments(), NewLineGrammars.TryParsePostfixTableWithComments, NewLineGrammars.TryParsePostfixTableWithComments, input);

    [Fact]
    public void GeneratedChoicesFindTheNewLines()
    {
        Assert.True(NewLineGrammars.TryParseArrowChoice("a => b", out var value));
        Assert.Equal("a=>b", value);
        Assert.True(NewLineGrammars.TryParseArrowChoice("a\n=> b", out value));
        Assert.Equal("a|=>b", value);
        Assert.True(NewLineGrammars.TryParseArrowChoiceWithComments("a /* \n */ => b", out value));
        Assert.Equal("a|=>b", value);
        Assert.True(NewLineGrammars.TryParsePostfixTable("a ++ * b\n++c", out value));
        Assert.Equal("((a++)*b)|++c", value);
    }

    [Theory]
    [InlineData("a   x")]
    [InlineData("a \n x")]
    [InlineData("a   +x")]
    [InlineData("a /* c */ x")]
    [InlineData("a /* \n */ =>")]
    [InlineData("a   ")]
    public void GeneratedChoicesFailWhereTheRuntimeOnesDo(string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.False(RuntimeNewLineGrammars.ArrowChoiceWithComments().Parse(context, ref result));
        Assert.False(NewLineGrammars.TryParseArrowChoiceWithComments(input, out _, out int furthest));
        Assert.Equal(context.Scanner.Cursor.FurthestOffset, furthest);
    }

    // The outcome of the runtime parser, from a string, is the one of the generated parser from a string and from readers,
    // an error included
    private static void CheckOutcome(Parser<string> parser, StringParser<string> parseString, ReaderParser<string> parseReader, string input)
    {
        var expected = Outcome(() => parser.TryParse(input, out var value) ? value! : "FAIL");

        Assert.Equal(expected, Outcome(() => parseString(input, out var value) ? value! : "FAIL"));

        foreach (var chunk in new[] { 1, 3, int.MaxValue })
        {
            Assert.Equal(expected, Outcome(() => parseReader(new VectorizedTokenTests.ChunkedReader(input, chunk), out var value) ? value! : "FAIL"));
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
    public static partial bool TryParseArrowChoice(string text, out string value);
    public static partial bool TryParseArrowChoice(TextReader reader, out string value);
    public static partial bool TryParseArrowChoiceWithComments(string text, out string value);
    public static partial bool TryParseArrowChoiceWithComments(TextReader reader, out string value);
    public static partial bool TryParseArrowChoiceWithComments(string text, out string value, out int furthest);
    public static partial bool TryParsePostfixTable(string text, out string value);
    public static partial bool TryParsePostfixTable(TextReader reader, out string value);
    public static partial bool TryParsePostfixTableWithComments(string text, out string value);
    public static partial bool TryParsePostfixTableWithComments(TextReader reader, out string value);
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

    // The values aren't used: the parsers are generated without them
    public static Parser<int> Captured() =>
        Capture(Terms.Identifier().And(Terms.Identifier().NoNewLineBefore()).And(NewLineAhead())).Then(static x => x.Length);

    // The new lines of the ASCII set: \v is one, U+2028 isn't white space
    public static Parser<string> AsciiSet() =>
        Terms.Identifier().And(Terms.Identifier().NoNewLineBefore().Then(static x => " " + x.ToString()).Else(""))
            .Then(static x => x.Item1.ToString() + x.Item2)
            .WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

    public static Parser<string> ArrowChoice()
    {
        var assignment = OneOf(Terms.Text("="), Terms.Text("+=")).And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2.ToString());
        var arrow = NoNewLineBefore(Terms.Text("=>")).SkipAnd(Terms.Identifier()).Then(static x => "=>" + x.ToString());
        var arrowAfterNewLine = Terms.Text("=>").SkipAnd(Terms.Identifier()).Then(static x => "|=>" + x.ToString());
        return Terms.Identifier().And(OneOf(assignment, arrow, arrowAfterNewLine)).Then(static x => x.Item1.ToString() + x.Item2).Eof();
    }

    public static Parser<string> ArrowChoiceWithComments() =>
        ArrowChoice().WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));

    public static Parser<string> PostfixTable()
    {
        var table = Terms.Identifier().Then(static x => x.ToString()).Operators()
            .Postfix((NoNewLineBefore(Terms.Text("++")), static x => "(" + x + "++)"), (NoNewLineBefore(Terms.Text("--")), static x => "(" + x + "--)"))
            .LeftAssociative((Terms.Char('*'), static (a, b) => "(" + a + "*" + b + ")"))
            .LeftAssociative((Terms.Char('+'), static (a, b) => "(" + a + "+" + b + ")"));
        var prefix = Terms.Text("++").SkipAnd(Terms.Identifier()).Then(static x => "++" + x.ToString());
        return ZeroOrMany(OneOf(table, prefix)).Eof().Then(static x => string.Join("|", x));
    }

    public static Parser<string> PostfixTableWithComments() =>
        PostfixTable().WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));
}
