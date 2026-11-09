#nullable enable

using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

// WithComments builds a CommentsParser, which skips white space and comments in a single loop. It must skip what the
// composition it replaces, Capture(ZeroOrMany(OneOf(...))) of the builder's parsers, skips.
public class CommentsParserTests
{
    private static readonly Parser<IReadOnlyList<TextSpan>> _identifiers = ZeroOrMany(Terms.Identifier());

    private static readonly Action<CommentsBuilder> _javaScript = static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/");

    private static readonly Action<CommentsBuilder> _sql = static c => c.WithWhiteSpace().WithSingleLine("--").WithSingleLine("#").WithMultiLine("/*", "*/");

    // Comments only, no white space: the white space is then a char like another one
    private static readonly Action<CommentsBuilder> _commentsOnly = static c => c.WithSingleLine("#").WithMultiLine("(*", "*)");

    // A single line comment which starts like a multi line one, added first: it wins, as in the choice
    private static readonly Action<CommentsBuilder> _prefixes = static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("/").WithMultiLine("/*", "*/");

    // An unterminated multi line comment which is not one, then the comments added after it can start there
    private static readonly Action<CommentsBuilder> _notAComment = static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment).WithSingleLine("/").WithSingleLine("#");

    // An unterminated multi line comment which runs to the end of the text
    private static readonly Action<CommentsBuilder> _toEnd = static c => c.WithWhiteSpace().WithSingleLine("#").WithMultiLine("/*", "*/", UnterminatedComment.ToEnd).WithMultiLine("(*", "*)", UnterminatedComment.ToEnd);

    private static readonly Dictionary<string, (Action<CommentsBuilder> Comments, Parser<TextSpan> Composed)> _builders = new()
    {
        ["not-a-comment"] = (_notAComment, Capture(ZeroOrMany(OneOf(Literals.WhiteSpace(includeNewLines: true), Capture(Literals.Text("/*").And(AnyCharBefore(Literals.Text("*/"), canBeEmpty: true, failOnEof: true, consumeDelimiter: true))), Literals.Comments("/"), Literals.Comments("#"))))),
        ["to-end"] = (_toEnd, Capture(ZeroOrMany(OneOf(Literals.WhiteSpace(), Literals.Comments("#"), Capture(Literals.Text("/*").And(AnyCharBefore(Literals.Text("*/"), canBeEmpty: true, failOnEof: false, consumeDelimiter: true))), Capture(Literals.Text("(*").And(AnyCharBefore(Literals.Text("*)"), canBeEmpty: true, failOnEof: false, consumeDelimiter: true))))))),
        ["javascript"] = (_javaScript, Capture(ZeroOrMany(OneOf(Literals.WhiteSpace(includeNewLines: true), Literals.Comments("//"), Literals.Comments("/*", "*/"))))),
        ["sql"] = (_sql, Capture(ZeroOrMany(OneOf(Literals.WhiteSpace(), Literals.Comments("--"), Literals.Comments("#"), Literals.Comments("/*", "*/"))))),
        ["comments-only"] = (_commentsOnly, Capture(ZeroOrMany(OneOf(Literals.Comments("#"), Literals.Comments("(*", "*)"))))),
        ["prefixes"] = (_prefixes, Capture(ZeroOrMany(OneOf(Literals.WhiteSpace(includeNewLines: true), Literals.Comments("/"), Literals.Comments("/*", "*/"))))),
    };

    public static TheoryData<string, string> Inputs()
    {
        var texts = new[]
        {
            "", "a", " a ", "a b", "a // b\n c", "a // b\r\n c", "a // b\r c", "a // b\u2028 c", "a //", "a // b", "//", "// a\n",
            "a /* b */ c", "a /**/ c", "a/*b*//*c*/d", "a /* b * / */ c", "a /* b", "a /*", "a /", "a / b", "/*/ a", "/* a */",
            "a -- b\n c", "a # b\n c", "a#b\nc", "a\n#b\n--c\nd", "a\tb", "a\r\n\r\nb", "a (* b *) c", "a(*b*)c", "a(*b", "a(b",
            "#", "(*", "(**)", "a\u00A0b", "a /* b */\n// c\n d /* e", "  ", "\n",
        };

        var data = new TheoryData<string, string>();

        foreach (var builder in _builders.Keys)
        {
            foreach (var text in texts)
            {
                data.Add(builder, text);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void ShouldSkipWhatTheComposedParsersSkip(string builder, string text)
    {
        var (comments, composed) = _builders[builder];

        Assert.Equal(Describe(_identifiers.WithWhiteSpaceParser(composed), text), Describe(_identifiers.WithComments(comments), text));
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void ShouldCollectTheDiagnosticsOfTheComposedParsers(string builder, string text)
    {
        var (comments, composed) = _builders[builder];

        Assert.Equal(DescribeDiagnostics(_identifiers.WithWhiteSpaceParser(composed), text), DescribeDiagnostics(_identifiers.WithComments(comments), text));
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void ShouldSkipFromTheStartWhatTheComposedParsersSkip(string builder, string text)
    {
        var (comments, composed) = _builders[builder];
        var builtComments = new CommentsBuilder();
        comments(builtComments);
        var parser = builtComments.Build();

        Assert.IsType<CommentsParser>(parser);

        // The parser fails when it skips nothing, where the composition succeeds with an empty span
        var context = new ParseContext(new Scanner(text));
        var expectedContext = new ParseContext(new Scanner(text));
        ParseResult<TextSpan> result = default, expected = default;

        Exception? exception = null, expectedException = null;
        bool success = false, expectedSuccess = false;

        try { success = parser.Parse(context, ref result); } catch (ParseException e) { exception = e; }
        try { expectedSuccess = composed.Parse(expectedContext, ref expected); } catch (ParseException e) { expectedException = e; }

        Assert.Equal(expectedException?.Message, exception?.Message);

        if (exception is not null)
        {
            Assert.Equal(((ParseException)expectedException!).Position, ((ParseException)exception).Position);
            return;
        }

        Assert.True(expectedSuccess);
        Assert.Equal(expected.Value.Length > 0, success);
        Assert.Equal(expectedContext.Scanner.Cursor.Offset, context.Scanner.Cursor.Offset);

        if (success)
        {
            Assert.Equal(expected.Value.ToString(), result.Value.ToString());
            Assert.Equal(expected.Start, result.Start);
            Assert.Equal(expected.End, result.End);
        }
    }

    [Theory]
    [InlineData("x")]
    [InlineData("")]
    [InlineData("/x")]
    [InlineData("/")]
    public void ShouldFailWithoutMovingTheCursorWhenNothingIsSkipped(string text)
    {
        var parser = Build(_javaScript);
        var context = new ParseContext(new Scanner(text));
        ParseResult<TextSpan> result = default;

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldThrowOnUnterminatedMultiLineComments()
    {
        var parser = Terms.Text("a").And(Terms.Text("b")).WithComments(_javaScript);

        var exception = Assert.Throws<ParseException>(() => parser.Parse("a\n /* b"));

        Assert.Equal("End-of-file found, '*/' expected", exception.Message);
        Assert.Equal(new TextPosition(5, 2, 4), exception.Position);
    }

    [Theory]
    [InlineData("a /* b */ c", "a|c")]
    [InlineData("a /* b", "a|/|*|b")]
    [InlineData("a/*", "a|/|*")]
    [InlineData("/**/ a /* b", "a|/|*|b")]
    [InlineData("a /* b /**/ c", "a|c")]
    public void ShouldReadUnterminatedMultiLineCommentsAsText(string text, string expected)
    {
        // xmSQL, as its ANTLR lexer: an unclosed "/*" is a '/' token and a '*' token
        var parser = ZeroOrMany(OneOf(Terms.Identifier(), Terms.AnyOf("/*", maxSize: 1)))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment));

        Assert.Equal(expected, string.Join("|", parser.Parse(text)!));

        var context = new ParseContext(new Scanner(text)) { CollectDiagnostics = true };
        ParseResult<IReadOnlyList<TextSpan>> result = default;

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(expected, string.Join("|", result.Value));
        Assert.Empty(context.Diagnostics);
    }

    [Theory]
    [InlineData("/* a")]
    [InlineData("/*")]
    public void ShouldFailWithoutMovingTheCursorOnAnUnterminatedCommentWhichIsNotOne(string text)
    {
        var parser = Build(static c => c.WithWhiteSpace().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment));
        var context = new ParseContext(new Scanner(text));
        ParseResult<TextSpan> result = default;

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // The white space before it is skipped
        context = new ParseContext(new Scanner("  " + text));

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("  ", result.Value.ToString());
        Assert.Equal(2, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("a /* b */ c", "a|c")]
    [InlineData("a /* b", "a")]
    [InlineData("a /* b\n c", "a")]
    [InlineData("a /*", "a")]
    public void ShouldSkipUnterminatedMultiLineCommentsToTheEnd(string text, string expected)
    {
        // Eof() doesn't skip white space: an empty skipped parser does
        var parser = _identifiers.AndSkip(SkipWhiteSpace(Always())).Eof().WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.ToEnd));

        Assert.Equal(expected, string.Join("|", parser.Parse(text)!));

        var context = new ParseContext(new Scanner(text)) { CollectDiagnostics = true };
        ParseResult<IReadOnlyList<TextSpan>> result = default;

        Assert.True(parser.Parse(context, ref result));
        Assert.Empty(context.Diagnostics);
        Assert.Equal(text.Length, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldThrowOnUnterminatedMultiLineCommentsByDefault()
    {
        var parser = Terms.Text("a").And(Terms.Text("b")).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.Throw));

        var exception = Assert.Throws<ParseException>(() => parser.Parse("a\n /* b"));

        Assert.Equal("End-of-file found, '*/' expected", exception.Message);
        Assert.Equal(new TextPosition(5, 2, 4), exception.Position);
    }

    [Fact]
    public void ShouldRejectAnUnknownUnterminatedComment()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommentsBuilder().WithMultiLine("/*", "*/", (UnterminatedComment)3));
    }

    [Theory]
    [InlineData(UnterminatedComment.NotAComment, "a /* b", false)]
    [InlineData(UnterminatedComment.ToEnd, "a /* b", true)]
    [InlineData(UnterminatedComment.NotAComment, "a /* b */", true)]
    public void ShouldComposeUnterminatedComments(UnterminatedComment unterminated, string text, bool expected)
    {
        // An empty end, or a parser added with WithParser(), composes the parsers of the elements
        var builder = new CommentsBuilder().WithWhiteSpace().WithMultiLine("/*", "*/", unterminated).WithParser(Capture(Literals.Text("...")));
        var parser = builder.Build();

        Assert.IsNotType<CommentsParser>(parser);
        Assert.Equal(expected, _identifiers.AndSkip(SkipWhiteSpace(Always())).Eof().WithWhiteSpaceParser(parser).TryParse(text, out _));
    }

    [Fact]
    public void ShouldResetTheCursorOnUnterminatedMultiLineCommentsWhenCollectingDiagnostics()
    {
        var parser = Build(_javaScript);
        var context = new ParseContext(new Scanner("  /* a")) { CollectDiagnostics = true };
        ParseResult<TextSpan> result = default;

        // The white space before the comment is skipped
        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("  ", result.Value.ToString());
        Assert.Equal(2, context.Scanner.Cursor.Offset);

        var diagnostic = Assert.Single(context.Diagnostics);
        Assert.Equal("End-of-file found, '*/' expected", diagnostic.Message);
        Assert.Equal(4, diagnostic.Position.Offset);

        context = new ParseContext(new Scanner("/* a")) { CollectDiagnostics = true };

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("a // b\nc", "a|c")]
    [InlineData("a // b\r\nc", "a|c")]
    [InlineData("a // b\rc", "a|c")]
    [InlineData("a // b\u2028c", "a|c")]
    [InlineData("a // b\u2029c", "a|c")]
    [InlineData("a // b\u0085c", "a")]
    [InlineData("a // b", "a")]
    public void ShouldEndSingleLineCommentsAtTheLineTerminators(string text, string expected)
    {
        var parser = _identifiers.WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//", "\r\n\u2028\u2029"));

        Assert.Equal(expected, string.Join("|", parser.Parse(text)!));
    }

    [Theory]
    [InlineData("a // b\nc", "a|c")]
    [InlineData("a // b\r\nc", "a|c")]
    [InlineData("a // b\rc", "a")]
    [InlineData("a // b\u2028c", "a")]
    public void ShouldEndSingleLineCommentsAtNewLinesByDefault(string text, string expected)
    {
        var parser = _identifiers.WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//"));

        Assert.Equal(expected, string.Join("|", parser.Parse(text)!));
    }

    [Fact]
    public void ShouldNotSkipTheLineTerminator()
    {
        // Without white space the terminator stays: the comment is skipped up to it
        var parser = Terms.Text("a").And(Terms.Char('\r')).WithComments(static c => c.WithSingleLine("#", "\r"));

        Assert.True(parser.TryParse("a#b\r", out _));
        Assert.True(parser.TryParse("a#b\n\r", out _));
        Assert.False(parser.TryParse("a#b\n", out _));
        Assert.True(_identifiers.WithComments(static c => c.WithSingleLine("#", "\n")).TryParse("a#b", out var value));
        Assert.Equal("a", Assert.Single(value!).ToString());
    }

    [Fact]
    public void ShouldRequireLineTerminators()
    {
        Assert.Throws<ArgumentException>(() => new CommentsBuilder().WithSingleLine("//", ""));
        Assert.Throws<ArgumentNullException>(() => new CommentsBuilder().WithSingleLine("//", null!));
        Assert.Throws<ArgumentNullException>(() => new CommentsBuilder().WithSingleLine(null!));
        Assert.Throws<ArgumentNullException>(() => new CommentsBuilder().WithMultiLine("/*", null!));
    }

    [Fact]
    public void ShouldComposeCustomParsers()
    {
        var builder = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("//", "\r\n").WithParser(Capture(Literals.Text("...")));
        var parser = builder.Build();

        Assert.IsNotType<CommentsParser>(parser);

        var grammar = _identifiers.WithWhiteSpaceParser(parser);

        Assert.Equal("a|b|c", string.Join("|", grammar.Parse("a ... b // x\r c")!));

        // The composed parser fails too when there is nothing to skip
        Assert.False(parser.TryParse("x", out _));
    }

    [Theory]
    [InlineData(" --")]
    [InlineData("")]
    public void ShouldComposeCommentsStartingWithWhiteSpace(string start)
    {
        var builder = new CommentsBuilder().WithWhiteSpace().WithSingleLine(start);

        Assert.IsNotType<CommentsParser>(builder.Build());
    }

    [Fact]
    public void ShouldComposeCommentsWithAnEmptyEnd()
    {
        Assert.IsNotType<CommentsParser>(new CommentsBuilder().WithMultiLine("/*", "").Build());
    }

    [Fact]
    public void ShouldUseTheNewLinesOfTheBuilder()
    {
        // WithWhiteSpace() doesn't skip new lines, a Terms parser can read them
        var parser = Terms.Text("a").AndSkip(Terms.Char('\n')).And(Terms.Text("b")).WithComments(static c => c.WithWhiteSpace().WithSingleLine("--"));

        Assert.True(parser.TryParse("a -- x\n b", out _));
        Assert.False(parser.TryParse("a\n\nb", out _));
    }

    [Fact]
    public void ShouldSkipCommentsAtTheEndOfTheInput()
    {
        // Eof() doesn't skip white space: an empty skipped parser does
        var parser = _identifiers.AndSkip(SkipWhiteSpace(Always())).Eof().WithComments(_javaScript);

        Assert.Equal("a|b", string.Join("|", parser.Parse("a b // c")!));
        Assert.Equal("a|b", string.Join("|", parser.Parse("a b /* c */")!));
        Assert.Equal("a|b", string.Join("|", parser.Parse("a b /* c */ ")!));
        Assert.Equal("a|b", string.Join("|", parser.Parse("a b //")!));
    }

    [Theory]
    [InlineData("a (* x *) b (* y *) c /* z */ d", true)]
    [InlineData("a b c d", true)]
    [InlineData("a /* x */ b c d", false)]
    [InlineData("a b /* y */ c d", false)]
    [InlineData("a b c (* x *) d", false)]
    public void ShouldRestoreNestedComments(string text, bool expected)
    {
        var inner = Terms.Text("b").And(Terms.Text("c")).WithComments(static c => c.WithWhiteSpace().WithMultiLine("(*", "*)"));
        var parser = Terms.Text("a").And(inner).And(Terms.Text("d")).WithComments(static c => c.WithWhiteSpace().WithMultiLine("/*", "*/"));

        Assert.Equal(expected, parser.TryParse(text, out _));
    }

    [Theory]
    [InlineData("a (* x *) b c..d", true)]
    [InlineData("a b c.d", true)]
    [InlineData("a.b c.d", false)]
    [InlineData("a b c (* x *) d", false)]
    [InlineData("a b c d", false)]
    public void ShouldRestoreAnOuterWhiteSpaceParser(string text, bool expected)
    {
        var inner = Terms.Text("b").And(Terms.Text("c")).WithComments(static c => c.WithWhiteSpace().WithMultiLine("(*", "*)"));
        var parser = Terms.Text("a").And(inner).And(Terms.Text("d")).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        Assert.Equal(expected, parser.TryParse(text, out _));
    }

    [Fact]
    public void ShouldExposeTheCharsItStartsWith()
    {
        var parser = (ISeekable)Build(_sql);

        Assert.True(parser.CanSeek);
        Assert.False(parser.SkipWhitespace);
        Assert.Contains(' ', parser.ExpectedChars);
        Assert.Contains('\t', parser.ExpectedChars);
        Assert.DoesNotContain('\n', parser.ExpectedChars);
        Assert.Contains('-', parser.ExpectedChars);
        Assert.Contains('#', parser.ExpectedChars);
        Assert.Contains('/', parser.ExpectedChars);
        Assert.Equal(parser.ExpectedChars.Length, parser.ExpectedChars.Distinct().Count());

        var commentsOnly = (ISeekable)Build(_commentsOnly);
        Assert.Equal(['#', '('], commentsOnly.ExpectedChars);

        Assert.Contains('\n', ((ISeekable)Build(_javaScript)).ExpectedChars);
    }

    [Theory]
    [InlineData("/* a */", true)]
    [InlineData("// a", true)]
    [InlineData(" ", true)]
    [InlineData("/", false)]
    [InlineData("x", false)]
    public void ShouldBeAnAlternativeOfAChoice(string text, bool expected)
    {
        // The choice looks up the alternatives by their first chars
        var parser = OneOf(Build(_javaScript).Then(static _ => "comments"), Literals.Char('x').Then(static _ => "x"));

        Assert.Equal(expected, parser.TryParse(text, out var value) && value == "comments");
    }

    [Theory]
    [InlineData("a b c")]
    [InlineData("a /* b */ c // d\n e")]
    public void ShouldReportNoLoopForComments(string text)
    {
        var list = Deferred<IReadOnlyList<TextSpan>>();
        list.Parser = ZeroOrMany(Terms.Identifier());

        Assert.True(list.WithComments(_javaScript).TryParse(text, out _));
    }

    [Fact]
    public void ShouldBeNamed()
    {
        Assert.Equal("CommentsParser", Build(_javaScript).Name);
    }

    private static Parser<TextSpan> Build(Action<CommentsBuilder> comments)
    {
        var builder = new CommentsBuilder();
        comments(builder);
        return builder.Build();
    }

    private static string Describe(Parser<IReadOnlyList<TextSpan>> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));

        try
        {
            ParseResult<IReadOnlyList<TextSpan>> result = default;
            var success = parser.Parse(context, ref result);

            return $"{success} {context.Scanner.Cursor.Offset} {(success ? string.Join("|", result.Value) : "")}";
        }
        catch (ParseException e)
        {
            return $"{e.Message} {e.Position}";
        }
    }

    private static string DescribeDiagnostics(Parser<IReadOnlyList<TextSpan>> parser, string text)
    {
        var context = new ParseContext(new Scanner(text)) { CollectDiagnostics = true };
        ParseResult<IReadOnlyList<TextSpan>> result = default;
        var success = parser.Parse(context, ref result);

        return $"{success} {context.Scanner.Cursor.Offset} {(success ? string.Join("|", result.Value) : "")} {string.Join(";", context.Diagnostics.Select(static d => $"{d.Severity} {d.Position} {d.Message}"))}";
    }
}
