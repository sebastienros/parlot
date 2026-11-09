#nullable enable

using Parlot.Fluent;
using System;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// <see cref="NoNewLineBefore{T}"/> and <see cref="NewLineAhead"/>, which tell whether the white space and comments before
/// the next token contain a new line.
/// </summary>
public class NewLineTests
{
    private static readonly Parser<string> Return = Terms.Keyword("return")
        .SkipAnd(Terms.Identifier().NoNewLineBefore().Then(static x => "return " + x).Else("return"));

    private static Parser<T> WithComments<T>(Parser<T> parser)
        => parser.WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));

    [Theory]
    [InlineData("return a", "return a")]
    [InlineData("return\ta", "return a")]
    [InlineData("return\na", "return")]
    [InlineData("return\r\na", "return")]
    [InlineData("return\ra", "return")]
    [InlineData("return \u2028 a", "return")]
    [InlineData("return \u2029 a", "return")]
    [InlineData("return", "return")]
    [InlineData("return ", "return")]
    [InlineData("return \n", "return")]
    public void NoNewLineBeforeShouldFailOnANewLine(string input, string expected)
    {
        Assert.Equal(expected, Return.Parse(input));
    }

    [Theory]
    [InlineData("return /* x */ a", "return a")]
    [InlineData("return /* x \n y */ a", "return")]
    [InlineData("return // x\n a", "return")]
    [InlineData("return /* x */\n a", "return")]
    [InlineData("return\n/* x */ a", "return")]
    [InlineData("return /* x */ /* y */ a", "return a")]
    public void NoNewLineBeforeShouldSeeTheNewLinesOfComments(string input, string expected)
    {
        Assert.Equal(expected, WithComments(Return).Parse(input));
    }

    [Fact]
    public void NoNewLineBeforeShouldUseACustomWhiteSpaceParser()
    {
        // The white space parser only skips spaces and '#' comments up to the end of the line, the new line included
        var whiteSpace = Capture(ZeroOrMany(OneOf(Literals.Char(' ').Then(static _ => 0), Literals.Char('#').And(AnyCharBefore(Literals.Char('\n'))).And(Literals.Char('\n')).Then(static _ => 0))));
        var parser = Return.WithWhiteSpaceParser(whiteSpace);

        Assert.Equal("return a", parser.Parse("return  a"));
        Assert.Equal("return", parser.Parse("return # x\n a"));
    }

    [Fact]
    public void NoNewLineBeforeShouldRestoreTheCursorOnFailure()
    {
        var parser = Terms.Identifier().NoNewLineBefore();

        // A new line before the identifier
        var context = new ParseContext(new Scanner("  \n  a"));
        var result = new ParseResult<TextSpan>();
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // No new line, but no identifier
        context = new ParseContext(new Scanner("   1"));
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // A new line in a comment
        context = new ParseContext(new Scanner(" /* \n */ a")) { WhiteSpaceParser = Capture(ZeroOrMany(Literals.Comments("/*", "*/").Or(Literals.WhiteSpace(includeNewLines: true)))) };
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // Matches
        context = new ParseContext(new Scanner(" \t a b"));
        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("a", result.Value.ToString());
        Assert.Equal(4, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void NoNewLineBeforeShouldUseTheNewLinesOfTheWhiteSpaceSet()
    {
        // \v and U+0085 are new lines of the default set, white space of none of the others
        Assert.Equal("return", Return.Parse("return \v a"));
        Assert.Equal("return", Return.Parse("return \u0085 a"));
        Assert.Equal("return", Return.WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Parse("return \v a"));
        Assert.Equal("return", Return.WithWhiteSpaceSet(WhiteSpaceSet.SpaceTabCrLf).Parse("return \n a"));

        // With UseNewLines the new lines aren't skipped: the identifier isn't there
        var context = new ParseContext(new Scanner("return \n a"), useNewLines: true);
        Assert.Equal("return", Return.Parse(context));
        Assert.Equal(6, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void NoNewLineBeforeShouldUseCustomNewLines()
    {
        // JavaScript's line terminators: \v is white space there
        var parser = Terms.Keyword("return").SkipAnd(Terms.Identifier().NoNewLineBefore("\n\r\u2028\u2029").Then(static x => "return " + x).Else("return"));

        Assert.Equal("return a", parser.Parse("return \v a"));
        Assert.Equal("return", parser.Parse("return \u2028 a"));
        Assert.Equal("return", parser.Parse("return \r a"));

        Assert.Throws<ArgumentException>(() => Terms.Identifier().NoNewLineBefore(""));
        Assert.Throws<ArgumentNullException>(() => Terms.Identifier().NoNewLineBefore(null!));
        Assert.Throws<ArgumentNullException>(() => NoNewLineBefore<TextSpan>(null!));
    }

    [Fact]
    public void NoNewLineBeforeShouldWorkInAChoice()
    {
        // The choice skips the white space once for its alternatives, NoNewLineBefore searches the white space it skipped
        var choice = OneOf(NoNewLineBefore(Terms.Text("++")), NoNewLineBefore(Terms.Text("--")), Terms.Text("!"));
        var postfix = Terms.Identifier().And(choice.Else(""))
            .Then(static x => x.Item1.ToString() + x.Item2);

        Assert.Equal("a++", postfix.Parse("a ++"));
        Assert.Equal("a++", postfix.Parse("a++"));
        Assert.Equal("a--", postfix.Parse("a --"));
        Assert.Equal("a!", postfix.Parse("a\n!"));
        Assert.Equal("a", postfix.Parse("a\n++"));
        Assert.Equal("a", postfix.Parse("a \n\t--"));

        // The choice has a lookup table
        var seekable = (Parlot.Rewriting.ISeekable)choice;
        Assert.True(seekable.CanSeek);
        Assert.True(seekable.SkipWhitespace);
        Assert.Equal(['!', '+', '-'], seekable.ExpectedChars.OrderBy(static c => c));

        // Wrapping the choice keeps its lookup table
        var wrapped = Terms.Identifier().And(NoNewLineBefore(OneOf(Terms.Text("++"), Terms.Text("--"))).Else(""))
            .Then(static x => x.Item1.ToString() + x.Item2);

        Assert.Equal("a--", wrapped.Parse("a --"));
        Assert.Equal("a", wrapped.Parse("a\n--"));
    }

    [Fact]
    public void NoNewLineBeforeShouldSeeTheNewLineOfACommentSkippedByAChoice()
    {
        // The white space the choice skipped is a comment holding a new line
        var choice = OneOf(NoNewLineBefore(Terms.Text("++")), Terms.Text("!"));
        var postfix = WithComments(Terms.Identifier().And(choice.Else("")).Then(static x => x.Item1.ToString() + x.Item2));

        Assert.Equal("a++", postfix.Parse("a /* c */ ++"));
        Assert.Equal("a", postfix.Parse("a /* \n */ ++"));
        Assert.Equal("a", postfix.Parse("a // c\n++"));
        Assert.Equal("a!", postfix.Parse("a /* \n */ !"));
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("a++", "(a++)")]
    [InlineData("a ++ !", "((a++)!)")]
    [InlineData("a\n++", "a")]
    [InlineData("a !\n++", "(a!)")]
    [InlineData("a.b ++", "((a.b)++)")]
    [InlineData("a\n.b\n++", "(a.b)")]
    public void NoNewLineBeforeShouldBeAPostfixOperatorOfATable(string input, string expected)
    {
        // The member accesses bind the tightest, then the postfix operators
        var expression = Terms.Identifier().Then(static x => x.ToString()).Operators()
            .Postfix((Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => $"({x}.{name})"))
            .Postfix(
                (NoNewLineBefore(Terms.Text("++")), static x => $"({x}++)"),
                (Terms.Text("!"), static x => $"({x}!)"));

        Assert.Equal(expected, expression.Parse(input));

        foreach (var chunk in new[] { 1, 2, 7 })
        {
            Assert.Equal(expected, expression.Parse(new StreamingTests.ChunkedReader(input, () => chunk, false)));
        }
    }

    [Theory]
    [InlineData("a\nb", true)]
    [InlineData("a\r\nb", true)]
    [InlineData("a  \n  b", true)]
    [InlineData("a\n", true)]
    [InlineData("a b", false)]
    [InlineData("a", false)]
    [InlineData("a   ", false)]
    [InlineData("a.", false)]
    public void NewLineAheadShouldNotConsume(string input, bool expected)
    {
        var context = new ParseContext(new Scanner(input));
        Terms.Identifier().Parse(context);

        var result = new ParseResult<object?>();
        Assert.Equal(expected, NewLineAhead().Parse(context, ref result));
        Assert.Null(result.Value);

        // The cursor is where it was, whether it matched or not
        Assert.Equal(1, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("a /* x */ b", false)]
    [InlineData("a /* x \n */ b", true)]
    [InlineData("a // x\n b", true)]
    [InlineData("a // x", false)]
    public void NewLineAheadShouldSeeTheNewLinesOfComments(string input, bool expected)
    {
        var parser = WithComments(Terms.Identifier().And(NewLineAhead()).Then(static _ => true).Else(false));
        Assert.Equal(expected, parser.Parse(input));
    }

    [Fact]
    public void NewLineAheadShouldReportAnUnterminatedComment()
    {
        // The comment parser reports the error, as it does for the next token
        var parser = WithComments(Terms.Identifier().And(NewLineAhead()).Then(static _ => true).Else(false));
        Assert.Throws<ParseException>(() => parser.Parse("a /* x"));
    }

    [Fact]
    public void NewLineAheadShouldUseCustomNewLines()
    {
        var parser = Terms.Identifier().And(NewLineAhead("\n")).Then(static _ => true).Else(false);

        Assert.True(parser.Parse("a \n b"));
        Assert.False(parser.Parse("a \r b"));
        Assert.Throws<ArgumentException>(() => NewLineAhead(""));
        Assert.Throws<ArgumentNullException>(() => NewLineAhead(null!));
    }

    [Theory]
    [InlineData("a;b", "a|b")]
    [InlineData("a\nb", "a|b")]
    [InlineData("a\r\nb", "a|b")]
    [InlineData("a /* \n */ b", "a|b")]
    [InlineData("a // c\n b", "a|b")]
    [InlineData("a", "a")]
    [InlineData("a  ", "a")]
    [InlineData("return\na", "return|a")]
    [InlineData("return a\n++b", "return a|++b")]
    [InlineData("a\n++b", "a|++b")]
    [InlineData("a++\nb", "a++|b")]
    [InlineData("a b", null)]
    [InlineData("a /* c */ b", null)]
    [InlineData("a++ b", null)]
    public void ShouldParseAutomaticSemicolons(string input, string? expected)
    {
        Assert.Equal(expected, WithComments(CreateStatements()).Parse(input));
    }

    [Fact]
    public void DocumentationSamplesShouldWork()
    {
        var argument = Terms.Identifier().NoNewLineBefore("\n\r\u2028\u2029").Then(static x => x.ToString());
        var parser = Terms.Keyword("return").SkipAnd(argument.Else("(none)"))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

        Assert.Equal("x", parser.Parse("return x"));
        Assert.Equal("x", parser.Parse("return /* x */ x"));
        Assert.Equal("(none)", parser.Parse("return\nx"));
        Assert.Equal("(none)", parser.Parse("return /* \n */ x"));

        var end = OneOf(
            Terms.Char(';').Then<object?>(static _ => null),
            Always().WhenFollowedBy(Terms.Char('}')),
            SkipWhiteSpace(Eof()),
            NewLineAhead());

        var statement = Terms.Identifier().AndSkip(end);
        var statements = ZeroOrMany(statement).Eof();

        Assert.Equal(["a", "b"], statements.Parse("a; b")!.Select(static x => x.ToString()));
        Assert.Equal(["a", "b"], statements.Parse("a\nb")!.Select(static x => x.ToString()));
        Assert.Null(statements.Parse("a b"));
    }

    // Statements ended by a semicolon, a new line, a '}' or the end of the input, as JavaScript's automatic semicolon insertion
    internal static Parser<string> CreateStatements()
    {
        var end = OneOf(Terms.Char(';').Then<object?>(static _ => null), Always().WhenFollowedBy(Terms.Char('}')), SkipWhiteSpace(Eof()), NewLineAhead());
        var returnStatement = Terms.Keyword("return").SkipAnd(Terms.Identifier().NoNewLineBefore().Then(static x => "return " + x).Else("return"));
        var prefix = Terms.Text("++").SkipAnd(Terms.Identifier()).Then(static x => "++" + x);
        var expression = Terms.Identifier().And(NoNewLineBefore(Terms.Text("++")).Else("")).Then(static x => x.Item1.ToString() + x.Item2);
        return ZeroOrMany(OneOf(returnStatement, prefix, expression).AndSkip(end)).Eof().Then(static x => string.Join("|", x));
    }
}
