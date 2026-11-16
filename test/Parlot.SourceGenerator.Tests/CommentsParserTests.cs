#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public class CommentsParserTests
{
    private delegate bool StringParser(string text, out string value);
    private delegate bool ReaderParser(TextReader reader, out string value);

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("a // b\n c")]
    [InlineData("a // b\r c")]
    [InlineData("a // b\u2028c")]
    [InlineData("a // b\u2029c")]
    [InlineData("a // b")]
    [InlineData("a //")]
    [InlineData("a /* b */ c")]
    [InlineData("a/**/c/* d\n */e")]
    [InlineData("a /* b")]
    [InlineData("a /*")]
    [InlineData("a /")]
    [InlineData("a / b")]
    [InlineData("/* a */")]
    [InlineData("\n// a\r\n")]
    public void JavaScript_Comments_Match_The_Runtime(string text)
    {
        var parser = ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//", "\r\n\u2028\u2029").WithMultiLine("/*", "*/"))
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

        Check(parser, CommentsGrammars.TryParseJavaScript, CommentsGrammars.TryParseJavaScript, text);
    }

    [Theory]
    [InlineData("a b\nc")]
    [InlineData("a - b\nc")]
    [InlineData("a -- b\nc")]
    [InlineData("a # b\nc -* d\n e *- f")]
    [InlineData("a -* b")]
    [InlineData("a -* b *-\n c")]
    [InlineData("a\r\nb")]
    [InlineData("a\n\nb")]
    [InlineData("-")]
    public void Comments_Starting_With_The_Same_Char_Match_The_Runtime(string text)
    {
        var parser = Separated(Terms.Char('\n'), ZeroOrMany(Terms.Identifier()).Then(static list => string.Join("|", list.Select(static x => x.ToString()))))
            .WithComments(static c => c.WithWhiteSpace().WithSingleLine("-").WithSingleLine("--").WithSingleLine("#").WithMultiLine("-*", "*-"))
            .Then(static lines => string.Join(";", lines));

        Check(parser, CommentsGrammars.TryParseSql, CommentsGrammars.TryParseSql, text);
    }

    [Theory]
    [InlineData("a#b\nc")]
    [InlineData("a(*b*)c")]
    [InlineData("a (*b*)c")]
    [InlineData("a(*b")]
    [InlineData("a(b")]
    [InlineData("#")]
    [InlineData("(*")]
    public void Comments_Without_White_Space_Match_The_Runtime(string text)
    {
        var parser = ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithSingleLine("#").WithMultiLine("(*", "*)"))
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

        Check(parser, CommentsGrammars.TryParseCommentsOnly, CommentsGrammars.TryParseCommentsOnly, text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a /* b */ c")]
    [InlineData("a /* b")]
    [InlineData("a/*")]
    [InlineData("/*")]
    [InlineData("a /*- b\n c")]
    [InlineData("a /*- b */ c")]
    [InlineData("/**/ a /* b")]
    [InlineData("a /* b /**/ c")]
    [InlineData("a * / b")]
    public void Unterminated_Comments_Which_Are_Not_Comments_Match_The_Runtime(string text)
    {
        var parser = ZeroOrMany(OneOf(Terms.Identifier(), Terms.AnyOf("/*", maxSize: 1)))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment).WithSingleLine("/*-"))
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

        Check(parser, CommentsGrammars.TryParseNotAComment, CommentsGrammars.TryParseNotAComment, text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a /* b */ c")]
    [InlineData("a /* b")]
    [InlineData("a /* b\n c")]
    [InlineData("a/*")]
    [InlineData("/*")]
    [InlineData("a /**/ b /* c */ d /* e")]
    public void Unterminated_Comments_To_The_End_Match_The_Runtime(string text)
    {
        var parser = ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.ToEnd))
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

        Check(parser, CommentsGrammars.TryParseToEnd, CommentsGrammars.TryParseToEnd, text);
    }

    [Theory]
    [InlineData("a /* b", "a|/|*|b")]
    [InlineData("a /* b */ c", "a|c")]
    [InlineData("/*", "/|*")]
    public void Unterminated_Comments_Which_Are_Not_Comments_Are_Not_Reported(string text, string expected)
    {
        Assert.True(CommentsGrammars.TryParseNotACommentDiagnostics(text, out var value, out var errors));
        Assert.Equal(expected, value);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("a /* b", 2)]
    [InlineData("a /*", 2)]
    [InlineData("a\n  /* b\n", 4)]
    [InlineData("a / b", 2)]
    [InlineData("a /* b */ 1", 10)]
    public void Unterminated_Comments_Which_Are_Not_Comments_Are_Not_The_Furthest_Failure(string text, int offset)
    {
        // As at runtime, the failure is the token which doesn't match, not the position after the start of the comment
        var parser = Terms.Identifier().And(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment));
        var context = new ParseContext(new Scanner(text));

        Assert.False(parser.TryParse(context, out _, out _));
        Assert.Equal(offset, context.Scanner.Cursor.FurthestOffset);

        Assert.False(CommentsGrammars.TryParseNotACommentFailure(text, out _, out int failureOffset));
        Assert.Equal(offset, failureOffset);

        // The same when the builder composes the parsers of its elements
        Assert.False(CommentsGrammars.TryParseComposedNotACommentFailure(text, out _, out failureOffset));
        Assert.Equal(offset, failureOffset);

        for (var chunk = 1; chunk <= 3; chunk++)
        {
            Assert.False(CommentsGrammars.TryParseNotACommentFailure(new ChunkedReader(text, chunk), out _, out (int Offset, int Line, int Column) failure));
            Assert.Equal(offset, failure.Offset);

            Assert.False(CommentsGrammars.TryParseComposedNotACommentFailure(new ChunkedReader(text, chunk), out _, out failure));
            Assert.Equal(offset, failure.Offset);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("a /* b */ c")]
    [InlineData("a /* b")]
    [InlineData("a/*")]
    [InlineData("a ... b /* c")]
    [InlineData("a .. b")]
    [InlineData("a (* b *) c (* d\n e")]
    [InlineData("a (* b /* c")]
    [InlineData("a /* b (* c")]
    [InlineData("/**//**/(**)...a")]
    public void Composed_Unterminated_Comments_Match_The_Runtime(string text)
    {
        var parser = ZeroOrMany(OneOf(Terms.Identifier(), Terms.AnyOf("/*", maxSize: 1)))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment).WithParser(Capture(Literals.Text("..."))).WithMultiLine("(*", "*)", UnterminatedComment.ToEnd))
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

        Check(parser, CommentsGrammars.TryParseComposedUnterminated, CommentsGrammars.TryParseComposedUnterminated, text);
    }

    [Fact]
    public void Unterminated_Comments_Fail()
    {
        Assert.False(CommentsGrammars.TryParseJavaScript("a /* b", out var value));
        Assert.Null(value);
        Assert.False(CommentsGrammars.TryParseJavaScript(new StringReader("a /* b"), out value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData("a b", true, "a|b", "")]
    [InlineData("a /* b", true, "a", "End-of-file found, '*/' expected 4 1:5")]
    [InlineData("a\n  /*", true, "a", "End-of-file found, '*/' expected 6 2:5")]
    [InlineData("/*", true, "", "End-of-file found, '*/' expected 2 1:3")]
    public void Unterminated_Comments_Are_Reported(string text, bool success, string expected, string error)
    {
        var parser = ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"))
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

        var context = new ParseContext(new Scanner(text)) { CollectDiagnostics = true };
        ParseResult<string> result = default;
        Assert.Equal(success, parser.Parse(context, ref result));
        Assert.Equal(expected, result.Value);
        Assert.Equal(error, string.Join(";", context.Diagnostics.Select(static d => $"{d.Message} {d.Position.Offset} {d.Position.Line}:{d.Position.Column}")));

        Assert.Equal(success, CommentsGrammars.TryParseDiagnostics(text, out var value, out var errors));
        Assert.Equal(expected, value);
        Assert.Equal(error, string.Join(";", errors.Select(static d => $"{d.Message} {d.Offset} {d.Line}:{d.Column}")));
    }

    // The chars of the grammars of CommentsGrammars, which the test compilation doesn't include
    private const string JavaScriptWhiteSpace = "\t\v\f \u00A0\uFEFF\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u202F\u205F\u3000";
    private const string JavaScriptNewLines = "\n\r\u2028\u2029";

    public static TheoryData<string> JavaScriptCharsInputs() =>
    [
        "", "a b", "a\uFEFFb", "\uFEFF a", "a\vb", "a\u000Cb", "a\u3000\u00A0b", "a\u0085b", "a \u0085 b", "\u0085", "a\u2028b",
        "a\u2029\r\nb", "a // b\u2028c", "a // \u0085 b\nc", "a /* \u2028 */ b", "a /* b", "a /", "a\u180Eb",
        // Across the 4096 chars the TextReader entry point reads at once
        "a" + new string('\uFEFF', 5000) + "b", "a" + new string(' ', 4093) + "\u2028\u3000\vb", "a" + new string('\u3000', 4094) + "\u0085b",
    ];

    [Theory]
    [MemberData(nameof(JavaScriptCharsInputs))]
    public void JavaScript_White_Space_Matches_The_Runtime(string text)
    {
        var parser = ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpace(JavaScriptWhiteSpace, JavaScriptNewLines).WithSingleLine("//", JavaScriptNewLines).WithMultiLine("/*", "*/"))
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

        Check(parser, CommentsGrammars.TryParseJavaScriptChars, CommentsGrammars.TryParseJavaScriptChars, text);
    }

    [Theory]
    [MemberData(nameof(JavaScriptCharsInputs))]
    [InlineData("a b\u2028c d")]
    [InlineData("a /* \u2029 */ b c")]
    [InlineData("a \uFEFF\v b")]
    public void JavaScript_New_Lines_Match_The_Runtime(string text)
    {
        var parser = ZeroOrMany(Terms.Identifier().And(ZeroOrMany(NoNewLineBefore(Terms.Identifier(), JavaScriptNewLines))).Then(static x => string.Join(" ", new[] { x.Item1.ToString() }.Concat(x.Item2.Select(static y => y.ToString())))))
            .WithComments(static c => c.WithWhiteSpace(JavaScriptWhiteSpace, JavaScriptNewLines).WithSingleLine("//", JavaScriptNewLines).WithMultiLine("/*", "*/"))
            .Eof()
            .Then(static list => string.Join("|", list));

        Check(parser, CommentsGrammars.TryParseJavaScriptCharsNoNewLine, CommentsGrammars.TryParseJavaScriptCharsNoNewLine, text);
    }

    [Theory]
    [InlineData("a\uFEFF\vb", "a b")]
    [InlineData("a b\u2028c /* \n */ d e", "a b|c|d e")]
    public void JavaScript_White_Space_Is_Skipped(string text, string expected)
    {
        Assert.True(CommentsGrammars.TryParseJavaScriptCharsNoNewLine(text, out var value));
        Assert.Equal(expected, value);
        Assert.False(CommentsGrammars.TryParseJavaScriptChars("a\u0085b", out _));
    }

    public static TheoryData<string> LineStartInputs()
    {
        var data = new TheoryData<string>
        {
            "", "-->", "--> a\nb", "  --> a\nb", "/* */ --> a\nb", "a\n--> b\nc", "a\r\n  --> b\u2028c", "a /*\n*/ --> b\nc",
            "a /*\r*/ --> b\nc", "a /*\u2028*/ --> b", "a /* b */ --> c", "a // b\n--> c\nd", "a <!-- b\u2029--> c\rd", "a --> b",
            "a-->b", "a /*\n --> b", "a\n--> b\n--> c\nd", "a # b\n--> c", "--> a\nb --> c", "a\u0085--> b", "a\v--> b",
            // Across the 4096 chars the TextReader entry point reads at once
            "a" + new string(' ', 4094) + "\n--> b\nc", "a /*" + new string(' ', 4094) + "\n*/ --> b\nc", "a" + new string(' ', 4093) + "--> b",
        };

        // Random texts of the chars of the tokens, the comments, the white space and new lines of both sets
        const string chars = "ab-->-<!/* \t\n\r\u2028\u2029\u0085\v\u00A0\uFEFF#";
        var random = new Random(585);

        for (var i = 0; i < 300; i++)
        {
            var text = new char[random.Next(25)];

            for (var j = 0; j < text.Length; j++)
            {
                text[j] = chars[random.Next(chars.Length)];
            }

            data.Add(new string(text));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LineStartInputs))]
    public void Comments_At_The_Start_Of_A_Line_Match_The_Runtime(string text)
    {
        var tokens = ZeroOrMany(OneOf(Terms.Identifier().Then(static x => x.ToString()), Terms.Text("--"), Terms.Text(">"), Terms.Text("<"), Terms.Text("!"), Terms.Text("/"), Terms.Text("*")));

        var script = tokens
            .AndSkip(SkipWhiteSpace(Always()))
            .Eof()
            .Then(static list => string.Join("|", list))
            .WithComments(static c => c.WithWhiteSpace(JavaScriptWhiteSpace, JavaScriptNewLines).WithSingleLine("//", JavaScriptNewLines).WithSingleLine("<!--", JavaScriptNewLines).WithSingleLine("-->", JavaScriptNewLines, CommentPlacement.LineStart).WithMultiLine("/*", "*/", UnterminatedComment.NotAComment, JavaScriptNewLines));

        Check(script, CommentsGrammars.TryParseJavaScriptScript, CommentsGrammars.TryParseJavaScriptScript, text);

        var scriptSet = tokens
            .AndSkip(SkipWhiteSpace(Always()))
            .Eof()
            .Then(static list => string.Join("|", list))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("-->", JavaScriptNewLines, CommentPlacement.LineStart).WithSingleLine("//", JavaScriptNewLines).WithSingleLine("<!--", JavaScriptNewLines).WithMultiLine("/*", "*/"));

        Check(scriptSet, CommentsGrammars.TryParseJavaScriptScriptSet, CommentsGrammars.TryParseJavaScriptScriptSet, text);

        var withoutNewLines = ZeroOrMany(OneOf(Terms.Identifier().Then(static x => x.ToString()), Terms.Text("--"), Terms.Text(">"), Terms.Text("\n")))
            .WithComments(static c => c.WithWhiteSpace().WithSingleLine("#", "\n").WithSingleLine("-->", "\n", CommentPlacement.LineStart))
            .Eof()
            .Then(static list => string.Join("|", list));

        Check(withoutNewLines, CommentsGrammars.TryParseLineStartWithoutNewLines, CommentsGrammars.TryParseLineStartWithoutNewLines, text);
    }

    [Theory]
    [InlineData("--> a\nb", "b")]
    [InlineData("a\u2028--> b\nc", "a|c")]
    [InlineData("a /*\r*/ --> b", "a")]
    [InlineData("a /* b */ --> c", "a|--|>|c")]
    public void Comments_At_The_Start_Of_A_Line_Are_Skipped(string text, string expected)
    {
        Assert.True(CommentsGrammars.TryParseJavaScriptScript(text, out var value));
        Assert.Equal(expected, value);
        Assert.True(CommentsGrammars.TryParseJavaScriptScript(new ChunkedReader(text, 1), out value));
        Assert.Equal(expected, value);
    }

    private static void Check(Parser<string> parser, StringParser parseString, ReaderParser parseReader, string input)
    {
        bool expected;
        string? expectedValue;

        try
        {
            expected = parser.TryParse(input, out expectedValue);
        }
        catch (ParseException)
        {
            // The generated entry points convert the error to a failure
            expected = false;
            expectedValue = null;
        }

        Assert.Equal(expected, parseString(input, out var value));
        Assert.Equal(expected ? expectedValue : default, value);

        foreach (var reader in new TextReader[] { new StringReader(input), new ChunkedReader(input, 1), new ChunkedReader(input, 3) })
        {
            Assert.Equal(expected, parseReader(reader, out value));
            Assert.Equal(expected ? expectedValue : default, value);
        }
    }

    // A reader returning at most a fixed number of chars per read
    private sealed class ChunkedReader : TextReader
    {
        private readonly string _text;
        private readonly int _chunk;
        private int _position;

        public ChunkedReader(string text, int chunk)
        {
            _text = text;
            _chunk = chunk;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var length = Math.Min(Math.Min(count, _chunk), _text.Length - _position);
            _text.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }

        public override int Read(Span<char> buffer)
        {
            var length = Math.Min(Math.Min(buffer.Length, _chunk), _text.Length - _position);
            _text.AsSpan(_position, length).CopyTo(buffer);
            _position += length;
            return length;
        }
    }
}

public static partial class CommentsGrammars
{
    public static partial bool TryParseJavaScript(string text, out string value);
    public static partial bool TryParseJavaScript(TextReader reader, out string value);
    public static partial bool TryParseJavaScriptChars(string text, out string value);
    public static partial bool TryParseJavaScriptChars(TextReader reader, out string value);
    public static partial bool TryParseJavaScriptCharsNoNewLine(string text, out string value);
    public static partial bool TryParseJavaScriptCharsNoNewLine(TextReader reader, out string value);
    public static partial bool TryParseJavaScriptScript(string text, out string value);
    public static partial bool TryParseJavaScriptScript(TextReader reader, out string value);
    public static partial bool TryParseJavaScriptScriptSet(string text, out string value);
    public static partial bool TryParseJavaScriptScriptSet(TextReader reader, out string value);
    public static partial bool TryParseLineStartWithoutNewLines(string text, out string value);
    public static partial bool TryParseLineStartWithoutNewLines(TextReader reader, out string value);
    public static partial bool TryParseSql(string text, out string value);
    public static partial bool TryParseSql(TextReader reader, out string value);
    public static partial bool TryParseCommentsOnly(string text, out string value);
    public static partial bool TryParseCommentsOnly(TextReader reader, out string value);
    public static partial bool TryParseNotAComment(string text, out string value);
    public static partial bool TryParseNotAComment(TextReader reader, out string value);
    public static partial bool TryParseToEnd(string text, out string value);
    public static partial bool TryParseToEnd(TextReader reader, out string value);
    public static partial bool TryParseNotACommentDiagnostics(string text, out string value,
        out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> errors);
    public static partial bool TryParseNotACommentFailure(string text, out string value, out int failureOffset);
    public static partial bool TryParseNotACommentFailure(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseComposedUnterminated(string text, out string value);
    public static partial bool TryParseComposedUnterminated(TextReader reader, out string value);
    public static partial bool TryParseComposedNotACommentFailure(string text, out string value, out int failureOffset);
    public static partial bool TryParseComposedNotACommentFailure(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseDiagnostics(string text, out string value,
        out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> errors);
}
