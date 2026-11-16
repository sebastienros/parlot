#nullable enable

using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

// A single line comment which only starts at the start of a line, JavaScript's "-->" in scripts (ECMAScript Annex B.1.1)
public class LineStartCommentTests
{
    private const string NewLines = CommentsParserTests.JavaScriptNewLines;

    // The tokens which "-->" is in the middle of a line: "a --> b" is "a-- > b"
    internal static readonly Parser<IReadOnlyList<string>> Tokens = ZeroOrMany(OneOf(Terms.Identifier().Then(static x => x.ToString()), Terms.Text("--"), Terms.Text(">"), Terms.Text("<"), Terms.Text("!"), Terms.Text("/"), Terms.Text("*")));

    // JavaScript's script trivia over its own chars
    internal static readonly Action<CommentsBuilder> JavaScriptChars = static c => c
        .WithWhiteSpace(CommentsParserTests.JavaScriptWhiteSpace, NewLines)
        .WithSingleLine("//", NewLines)
        .WithSingleLine("<!--", NewLines)
        .WithSingleLine("-->", NewLines, CommentPlacement.LineStart)
        .WithMultiLine("/*", "*/", UnterminatedComment.NotAComment, NewLines);

    // The same over the white space of the context's set, whose new lines are \n, \r, \v, \f, U+0085, U+2028 and U+2029
    internal static readonly Action<CommentsBuilder> JavaScriptSet = static c => c
        .WithWhiteSpaceOrNewLine()
        .WithSingleLine("//", NewLines)
        .WithSingleLine("<!--", NewLines)
        .WithSingleLine("-->", NewLines, CommentPlacement.LineStart)
        .WithMultiLine("/*", "*/", UnterminatedComment.NotAComment, NewLines);

    private static readonly Dictionary<string, Action<CommentsBuilder>> _builders = new()
    {
        ["chars"] = JavaScriptChars,
        ["set"] = JavaScriptSet,
    };

    private static string ParseTokens(Action<CommentsBuilder> builder, string text, bool useNewLines = false)
    {
        // The trivia at the end is skipped
        var parser = Tokens.AndSkip(SkipWhiteSpace(Always())).Eof().WithComments(builder);
        var context = new ParseContext(new Scanner(text), useNewLines: useNewLines);
        ParseResult<IReadOnlyList<string>> result = default;

        Assert.True(parser.Parse(context, ref result), text);
        Assert.Equal(text.Length, context.Scanner.Cursor.Offset);
        return string.Join("|", result.Value);
    }

    public static TheoryData<string, string, string> Cases()
    {
        string[][] cases =
        [
            // At the start of the text, also after white space or comments there
            ["--> a\nb", "b"],
            ["  --> a\nb", "b"],
            ["/* */ --> a\nb", "b"],
            ["-->", ""],
            // After a new line, a multi line comment containing one, and a single line comment, which ends at one
            ["a\n--> b\nc", "a|c"],
            ["a\n  --> b\nc", "a|c"],
            ["a /*\n*/ --> b\nc", "a|c"],
            ["a /* b */\n--> c\nd", "a|d"],
            ["a // b\n--> c\nd", "a|d"],
            ["a <!-- b\n--> c\nd", "a|d"],
            ["a\n/* b */ --> c\nd", "a|d"],
            ["a\n--> b\n--> c\nd", "a|d"],
            // In the middle of a line, after a multi line comment without a new line too
            ["a --> b", "a|--|>|b"],
            ["a-->b", "a|--|>|b"],
            ["a /* b */ --> c", "a|--|>|c"],
            ["a /* b */--> c", "a|--|>|c"],
            ["a <!-- b", "a"],
            // An unterminated multi line comment is not one: the tokens after it are read, then the new line is white space
            ["a /*\n --> b", "a|/|*"],
        ];

        var data = new TheoryData<string, string, string>();

        foreach (var builder in _builders.Keys)
        {
            foreach (var c in cases)
            {
                data.Add(builder, c[0], c[1]);

                // Each of JavaScript's line terminators
                foreach (var newLine in new[] { "\r", "\r\n", "\u2028", "\u2029" })
                {
                    data.Add(builder, c[0].Replace("\n", newLine), c[1]);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ShouldSkipCommentsAtTheStartOfALineOnly(string builder, string text, string expected)
    {
        Assert.Equal(expected, ParseTokens(_builders[builder], text));
    }

    [Theory]
    [InlineData("a  --> x", 1, 3)]
    [InlineData("a-->", 1, 1)]
    [InlineData("a /* b */ --> x", 1, 10)]
    public void ShouldLeaveTheCursorBeforeACommentInTheMiddleOfALine(string text, int start, int end)
    {
        foreach (var builder in _builders.Values)
        {
            var parser = Build(builder);
            var context = new ParseContext(new Scanner(text));
            context.Scanner.Cursor.Advance(start);
            ParseResult<TextSpan> result = default;

            Assert.Equal(start != end, parser.Parse(context, ref result));
            Assert.Equal(end, context.Scanner.Cursor.Offset);

            if (start != end)
            {
                Assert.Equal(text[start..end], result.Value.ToString());
            }
        }
    }

    [Theory]
    [InlineData("a\n--> b", "a|\n|--|>|b")]
    [InlineData("--> b\nc", "\n|c")]
    public void ShouldNotStartALineWhenTheNewLinesAreNotWhiteSpace(string text, string expected)
    {
        // With UseNewLines the new lines are not skipped: the chars of WithWhiteSpace(whiteSpace, newLines) stop before them.
        // The grammar reads them as tokens
        var parser = ZeroOrMany(OneOf(Terms.Identifier().Then(static x => x.ToString()), Terms.Text("--"), Terms.Text(">"), Terms.Text("\n")))
            .WithComments(JavaScriptChars);
        var context = new ParseContext(new Scanner(text), useNewLines: true);
        ParseResult<IReadOnlyList<string>> result = default;

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(expected, string.Join("|", result.Value));
    }

    [Theory]
    [InlineData("a /*\n*/ --> b\nc", "a|c")]
    [InlineData("a /*\r*/ --> b\nc", "a|c")]
    [InlineData("a /*\u0085*/ --> b", "a")]
    [InlineData("a /* b */ --> c", "a|--|>|c")]
    public void ShouldStartALineAfterAMultiLineCommentContainingANewLineOfTheSetByDefault(string text, string expected)
    {
        // A multi line comment without line terminators contains a new line when it contains one of the white space
        var parser = Tokens.WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/").WithSingleLine("-->", "\n", CommentPlacement.LineStart));

        Assert.Equal(expected, string.Join("|", parser.Parse(text)!));
    }

    [Theory]
    [InlineData("a /*\n*/ --> b\nc", "a|c")]
    [InlineData("a /*\r*/ --> b", "a|--|>|b")]
    [InlineData("a /*\u2028*/ --> b", "a|--|>|b")]
    public void ShouldStartALineAfterAMultiLineCommentContainingANewLineOfTheCharsByDefault(string text, string expected)
    {
        // The new lines of WithWhiteSpace(whiteSpace, newLines)
        var parser = Tokens.WithComments(static c => c.WithWhiteSpace(" ", "\n").WithMultiLine("/*", "*/").WithSingleLine("-->", "\n", CommentPlacement.LineStart));

        Assert.Equal(expected, string.Join("|", parser.Parse(text)!));

        // Without new lines, a comment contains none
        var withoutNewLines = Tokens.WithComments(static c => c.WithWhiteSpace(" ", "").WithMultiLine("/*", "*/").WithSingleLine("-->", "\n", CommentPlacement.LineStart));

        Assert.Equal("a|--|>|b", string.Join("|", withoutNewLines.Parse("a /*\n*/ --> b")!));
    }

    [Fact]
    public void ShouldNotStartALineAfterASingleLineCommentEndingAtWhiteSpace()
    {
        // The comment ends at a tab, which is white space: the line goes on
        var parser = Tokens.WithComments(static c => c.WithWhiteSpace(" \t", "\n").WithSingleLine("#", "\t\n").WithSingleLine("-->", "\n", CommentPlacement.LineStart));

        Assert.Equal("a|--|>|y", string.Join("|", parser.Parse("a #x\t--> y")!));
        Assert.Equal("a|z", string.Join("|", parser.Parse("a #x\n--> y\nz")!));
    }

    [Theory]
    [InlineData("a --> b\nc", "a|c")]
    [InlineData("a\n--> b\nc", "a|c")]
    [InlineData("a -- b\nc", "a|c")]
    public void ShouldTryTheCommentsAddedAfterOneInTheMiddleOfALine(string text, string expected)
    {
        // "-->" isn't a comment in the middle of a line, "--" is one anywhere
        var parser = Tokens.WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("-->", "\n", CommentPlacement.LineStart).WithSingleLine("--", "\n"));

        Assert.Equal(expected, string.Join("|", parser.Parse(text)!));
    }

    [Theory]
    [InlineData("a --> b", "a|--|>|b")]
    [InlineData("--> b\nc", "c")]
    [InlineData("a #x\n--> b\nc", "a|--|>|b|c")]
    public void ShouldStartALineWithoutWhiteSpace(string text, string expected)
    {
        // Without white space, only the start of the text starts a line: the grammar reads the white space and the new lines,
        // which end a single line comment, as tokens, and each skip starts after them
        var parser = ZeroOrMany(OneOf(Terms.Identifier().Then(static x => x.ToString()), Terms.Text("--"), Terms.Text(">"), Terms.Char(' ').Or(Terms.Char('\n')).Then(static _ => "_")))
            .WithComments(static c => c.WithSingleLine("#", "\n").WithSingleLine("-->", "\n", CommentPlacement.LineStart))
            .Then(static list => string.Join("|", list.Where(static x => x != "_")));

        Assert.Equal(expected, parser.Parse(text));
    }

    [Fact]
    public void ShouldStartALineAtTheStartOfTheTextOnly()
    {
        // Not at the start of the scanned text: a parse starting further, where the trivia doesn't contain a new line
        var parser = Build(JavaScriptChars);
        var context = new ParseContext(new Scanner("x --> a"));
        context.Scanner.Cursor.Advance(1);
        ParseResult<TextSpan> result = default;

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(2, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(3, 1, true)]
    [InlineData(1, 5, false)]
    public void ShouldStartALineWhereTheScannerStartsAtColumnOne(int line, int column, bool expected)
    {
        // A scanner created at the start of a line, as the windows of ParseManyAsync after a '\n'
        var parser = Build(JavaScriptChars);
        var context = new ParseContext(new Scanner("--> a\nb", new TextPosition(0, line, column), isFinal: true));
        ParseResult<TextSpan> result = default;

        Assert.Equal(expected, parser.Parse(context, ref result));
        Assert.Equal(expected ? 6 : 0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldKeepTheHintsOfTheComments()
    {
        var parser = (CommentsParser)Build(JavaScriptChars);

        Assert.Contains('-', parser.ExpectedChars);
        Assert.Contains('<', parser.ExpectedChars);
        Assert.Contains('/', parser.ExpectedChars);
    }

    [Fact]
    public void ShouldRequireASingleParserForCommentsAtTheStartOfALine()
    {
        Assert.Throws<InvalidOperationException>(() => new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("-->", "\n", CommentPlacement.LineStart).WithParser(Capture(Literals.Text("..."))).Build());
        Assert.Throws<InvalidOperationException>(() => new CommentsBuilder().WithWhiteSpace().WithSingleLine(" -->", "\n", CommentPlacement.LineStart).Build());
        Assert.Throws<InvalidOperationException>(() => new CommentsBuilder().WithWhiteSpace(" ", "\n").WithWhiteSpace("\t", "").WithSingleLine("-->", "\n", CommentPlacement.LineStart).Build());

        Assert.Throws<InvalidOperationException>(() => new CommentsBuilder().WithWhiteSpace().WithMultiLine("/*", "").WithSingleLine("-->", "\n", CommentPlacement.LineStart).Build());

        // A comment anywhere still composes
        Assert.IsNotType<CommentsParser>(new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("-->", "\n", CommentPlacement.Anywhere).WithParser(Capture(Literals.Text("..."))).Build());
    }

    [Fact]
    public void ShouldValidateTheArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommentsBuilder().WithSingleLine("-->", "\n", (CommentPlacement)2));
        Assert.Throws<ArgumentException>(() => new CommentsBuilder().WithSingleLine("-->", "", CommentPlacement.LineStart));
        Assert.Throws<ArgumentNullException>(() => new CommentsBuilder().WithSingleLine(null!, "\n", CommentPlacement.LineStart));
        Assert.Throws<ArgumentException>(() => new CommentsBuilder().WithMultiLine("/*", "*/", UnterminatedComment.Throw, ""));
        Assert.Throws<ArgumentNullException>(() => new CommentsBuilder().WithMultiLine("/*", "*/", UnterminatedComment.Throw, null!));
        Assert.Throws<ArgumentNullException>(() => new CommentsBuilder().WithMultiLine(null!, "*/", UnterminatedComment.Throw, "\n"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommentsBuilder().WithMultiLine("/*", "*/", (UnterminatedComment)3, "\n"));
    }

    [Fact]
    public void ShouldThrowOnUnterminatedMultiLineCommentsWithLineTerminators()
    {
        var parser = Tokens.WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.Throw, NewLines).WithSingleLine("-->", NewLines, CommentPlacement.LineStart));

        var exception = Assert.Throws<ParseException>(() => parser.Parse("a\n /* b"));

        Assert.Equal("End-of-file found, '*/' expected", exception.Message);
    }

    // The chars a random text is made of: tokens, comment delimiters, white space and new lines of both sets
    private const string RandomChars = "ab-->-<!/* \t\n\r\u2028\u2029\u0085\v\u00A0\uFEFF";

    public static TheoryData<string, int> RandomInputs()
    {
        var data = new TheoryData<string, int>();

        foreach (var builder in _builders.Keys)
        {
            for (var seed = 0; seed < 50; seed++)
            {
                data.Add(builder, seed);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RandomInputs))]
    public void ShouldSkipWhatAScannerOfTheTriviaSkips(string builder, int seed)
    {
        // A scanner written as the specification reads the trivia, compared on random texts
        var random = new Random(seed);
        var isChars = builder == "chars";

        for (var i = 0; i < 40; i++)
        {
            var text = new StringBuilder();

            for (var length = random.Next(30); length > 0; length--)
            {
                text.Append(RandomChars[random.Next(RandomChars.Length)]);
            }

            var input = text.ToString();
            var parser = Build(_builders[builder]);

            for (var start = 0; start <= input.Length; start++)
            {
                var context = new ParseContext(new Scanner(input));
                context.Scanner.Cursor.Advance(start);
                ParseResult<TextSpan> result = default;

                var success = parser.Parse(context, ref result);
                var expected = ScanTrivia(input, start, isChars);

                Assert.Equal(expected != start, success);
                Assert.Equal(expected, context.Scanner.Cursor.Offset);
            }
        }
    }

    // The end of the trivia from start: JavaScript's white space, line terminators, comments, and "-->" after a new line
    private static int ScanTrivia(string text, int start, bool isChars)
    {
        var lineStart = start == 0;
        var i = start;

        bool IsNewLine(char c) => NewLines.IndexOf(c) >= 0 || !isChars && Character.IsWhiteSpaceOrNewLine(c) && !Character.IsWhiteSpace(c);
        bool IsSpace(char c) => isChars ? CommentsParserTests.JavaScriptWhiteSpace.IndexOf(c) >= 0 : Character.IsWhiteSpace(c);

        while (i < text.Length)
        {
            var c = text[i];

            if (IsNewLine(c))
            {
                lineStart = true;
                i++;
            }
            else if (IsSpace(c))
            {
                i++;
            }
            else if (string.CompareOrdinal(text, i, "//", 0, 2) == 0 || string.CompareOrdinal(text, i, "<!--", 0, 4) == 0 || lineStart && string.CompareOrdinal(text, i, "-->", 0, 3) == 0)
            {
                while (i < text.Length && NewLines.IndexOf(text[i]) < 0)
                {
                    i++;
                }

                lineStart = true;
            }
            else if (string.CompareOrdinal(text, i, "/*", 0, 2) == 0 && text.IndexOf("*/", i + 2, StringComparison.Ordinal) is var end and >= 0)
            {
                lineStart |= text.AsSpan(i, end + 2 - i).IndexOfAny(NewLines.AsSpan()) >= 0;
                i = end + 2;
            }
            else
            {
                break;
            }
        }

        return i;
    }

    private static Parser<TextSpan> Build(Action<CommentsBuilder> comments)
    {
        var builder = new CommentsBuilder();
        comments(builder);
        var parser = builder.Build();

        Assert.IsType<CommentsParser>(parser);
        return parser;
    }
}
