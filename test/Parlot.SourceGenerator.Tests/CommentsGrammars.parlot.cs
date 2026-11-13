#nullable enable

using System.Linq;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Grammars skipping comments with WithComments, whose white space parser is a CommentsParser. Each has a string and a
// TextReader entry point, compared with the runtime parser in CommentsParserTests.
[IncludeUsings("System.Linq")]
public static partial class CommentsGrammars
{
    [GenerateParser(nameof(TryParseJavaScript))]
    private static Parser<string> BuildJavaScript() =>
        ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//", "\r\n\u2028\u2029").WithMultiLine("/*", "*/"))
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

    // Several comments starting with the same char, and new lines which are not white space
    [GenerateParser(nameof(TryParseSql))]
    private static Parser<string> BuildSql() =>
        Separated(Terms.Char('\n'), ZeroOrMany(Terms.Identifier()).Then(static list => string.Join("|", list.Select(static x => x.ToString()))))
            .WithComments(static c => c.WithWhiteSpace().WithSingleLine("-").WithSingleLine("--").WithSingleLine("#").WithMultiLine("-*", "*-"))
            .Then(static lines => string.Join(";", lines));

    // Comments only, the white space is a char like another one
    [GenerateParser(nameof(TryParseCommentsOnly))]
    private static Parser<string> BuildCommentsOnly() =>
        ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithSingleLine("#").WithMultiLine("(*", "*)"))
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

    // An unterminated comment read as tokens, then the comments added after it can start there
    [GenerateParser(nameof(TryParseNotAComment))]
    private static Parser<string> BuildNotAComment() =>
        ZeroOrMany(OneOf(Terms.Identifier(), Terms.AnyOf("/*", maxSize: 1)))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment).WithSingleLine("/*-"))
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

    // An unterminated comment running to the end of the text
    [GenerateParser(nameof(TryParseToEnd))]
    private static Parser<string> BuildToEnd() =>
        ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.ToEnd))
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

    // The diagnostics of an unterminated comment, and none for one which is not a comment
    [GenerateParser(nameof(TryParseNotACommentDiagnostics))]
    private static Parser<string> BuildNotACommentDiagnostics() =>
        ZeroOrMany(OneOf(Terms.Identifier(), Terms.AnyOf("/*", maxSize: 1)))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment))
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

    // The furthest failure after an unterminated comment which is not one: where a token doesn't match
    [GenerateParser(nameof(TryParseNotACommentFailure))]
    private static Parser<string> BuildNotACommentFailure() =>
        Terms.Identifier().And(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment))
            .Then(static x => x.Item1.ToString() + "|" + x.Item2.ToString());

    // The diagnostics of an unterminated comment
    [GenerateParser(nameof(TryParseDiagnostics))]
    private static Parser<string> BuildDiagnostics() =>
        ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"))
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));
}
