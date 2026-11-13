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
