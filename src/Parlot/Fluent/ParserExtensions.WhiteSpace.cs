using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

public static partial class ParserExtensions
{
    /// <summary>
    /// Builds a parser that sets a custom whitespace parser for the current parser.
    /// </summary>
    /// <typeparam name="T">The type of the parser result.</typeparam>
    /// <param name="parser">The parser to execute with the custom whitespace parser.</param>
    /// <param name="whiteSpaceParser">The custom whitespace parser to use.</param>
    /// <returns>A parser that uses the custom whitespace parser.</returns>
    public static Parser<T> WithWhiteSpaceParser<T>(this Parser<T> parser, Parser<TextSpan> whiteSpaceParser)
        => new WithWhiteSpaceParser<T>(parser, whiteSpaceParser);

    /// <summary>
    /// Builds a parser that selects the chars that are white space for the current parser.
    /// </summary>
    /// <typeparam name="T">The type of the parser result.</typeparam>
    /// <param name="parser">The parser to execute with the white space set.</param>
    /// <param name="whiteSpaceSet">The chars that are white space while <paramref name="parser"/> runs.</param>
    /// <returns>A parser that uses the white space set.</returns>
    /// <remarks>
    /// It sets <see cref="ParseContext.WhiteSpaceSet"/>, which keeps the white space fast path. Unlike a context option it is
    /// part of the grammar, so a source-generated parser honors it too.
    /// </remarks>
    public static Parser<T> WithWhiteSpaceSet<T>(this Parser<T> parser, WhiteSpaceSet whiteSpaceSet)
        => new WithWhiteSpaceSet<T>(parser, whiteSpaceSet);

    /// <summary>
    /// Builds a parser that sets comments for the current parser.
    /// </summary>
    /// <typeparam name="T">The type of the parser result.</typeparam>
    /// <param name="parser">The parser to execute with the custom whitespace parser.</param>
    /// <param name="commentsBuilder">The action to configure the comments builder.</param>
    /// <returns>A parser that uses white spaces, new lines and comments.</returns>
    /// <remarks>
    /// Here is an example of usage:
    /// <code>
    /// var parserWithComments = myParser.WithComments(comments =>
    /// {
    ///     comments.WithWhiteSpaceOrNewLine();
    ///     comments.WithSingleLine("//");
    ///     comments.WithMultiLine("/*", "*/");
    /// });
    /// </code>
    /// </remarks>
    public static Parser<T> WithComments<T>(this Parser<T> parser, Action<CommentsBuilder> commentsBuilder)
    {
        var builder = new CommentsBuilder();
        commentsBuilder(builder);
        return new WithWhiteSpaceParser<T>(parser, builder.Build());
    }
}

/// <summary>
/// Describes the white space and comments that <see cref="ParserExtensions.WithComments{T}(Parser{T}, Action{CommentsBuilder})"/> skips.
/// </summary>
public class CommentsBuilder
{
    // The parsers of each element, in the order they were added, for a builder with custom parsers
    private readonly List<Parser<TextSpan>> _parsers = [];

    // The comments, in the order they were added
    private readonly List<CommentsParser.CommentSyntax> _comments = [];

    private bool _whiteSpace;
    private bool _whiteSpaceOrNewLine;

    // Whether a parser was added with WithParser(), or an element CommentsParser can't skip in the same order
    private bool _composed;

    /// <summary>
    /// Initializes a builder whose first element is <paramref name="whiteSpaceParser"/>.
    /// </summary>
    [Obsolete("Use CommentsBuilder().WithParser(parser) instead.")]
    public CommentsBuilder(Parser<TextSpan> whiteSpaceParser)
    {
        WithParser(whiteSpaceParser);
    }

    /// <summary>
    /// Initializes an empty builder.
    /// </summary>
    public CommentsBuilder()
    {
    }

    /// <summary>
    /// Skips the white space of the context's <see cref="ParseContext.WhiteSpaceSet"/>, without new lines.
    /// </summary>
    public CommentsBuilder WithWhiteSpace()
    {
        _parsers.Add(Literals.WhiteSpace());
        _whiteSpace = true;
        return this;
    }

    /// <summary>
    /// Skips the white space and new lines of the context's <see cref="ParseContext.WhiteSpaceSet"/>.
    /// </summary>
    public CommentsBuilder WithWhiteSpaceOrNewLine()
    {
        _parsers.Add(Literals.WhiteSpace(includeNewLines: true));
        _whiteSpaceOrNewLine = true;
        return this;
    }

    /// <summary>
    /// Skips what a parser matches, tried in the order the elements were added.
    /// </summary>
    /// <remarks>
    /// The builder then composes the parsers of its elements in a loop, a choice of parsers invoked at each position, rather
    /// than skipping them in a single parser.
    /// </remarks>
    public CommentsBuilder WithParser(Parser<TextSpan> parser)
    {
        ThrowHelper.ThrowIfNull(parser, nameof(parser));

        _parsers.Add(parser);
        _composed = true;
        return this;
    }

    /// <summary>
    /// Skips single line comments, from <paramref name="singleLineStart"/> to the end of the line: before <c>"\r\n"</c> or
    /// <c>"\n"</c>, which are not part of the comment, or to the end of the text.
    /// </summary>
    public CommentsBuilder WithSingleLine(string singleLineStart)
    {
        ThrowHelper.ThrowIfNull(singleLineStart, nameof(singleLineStart));

        _parsers.Add(Literals.Comments(singleLineStart));
        AddComment(new CommentsParser.CommentSyntax(singleLineStart, end: null, lineTerminators: null));
        return this;
    }

    /// <summary>
    /// Skips single line comments, from <paramref name="singleLineStart"/> to the first of <paramref name="lineTerminators"/>,
    /// which is not part of the comment, or to the end of the text.
    /// </summary>
    /// <param name="singleLineStart">The text which starts a comment.</param>
    /// <param name="lineTerminators">The chars which end a comment, for instance <c>"\r\n\u2028\u2029"</c> for JavaScript or C#.</param>
    public CommentsBuilder WithSingleLine(string singleLineStart, string lineTerminators)
    {
        ThrowHelper.ThrowIfNull(singleLineStart, nameof(singleLineStart));
        ThrowHelper.ThrowIfNull(lineTerminators, nameof(lineTerminators));

        if (lineTerminators.Length == 0)
        {
            throw new ArgumentException("At least one line terminator is required.", nameof(lineTerminators));
        }

        _parsers.Add(Capture(Literals.Text(singleLineStart).And(AnyCharBefore(Literals.AnyOf(lineTerminators, maxSize: 1), canBeEmpty: true, failOnEof: false, consumeDelimiter: false))));
        AddComment(new CommentsParser.CommentSyntax(singleLineStart, end: null, lineTerminators));
        return this;
    }

    /// <summary>
    /// Skips multi line comments, from <paramref name="multiLineStart"/> to <paramref name="multiLineEnd"/> included. A
    /// comment without its end throws a <see cref="ParseException"/>, or reports an error when the diagnostics are collected.
    /// </summary>
    public CommentsBuilder WithMultiLine(string multiLineStart, string multiLineEnd)
        => WithMultiLine(multiLineStart, multiLineEnd, UnterminatedComment.Throw);

    /// <summary>
    /// Skips multi line comments, from <paramref name="multiLineStart"/> to <paramref name="multiLineEnd"/> included.
    /// </summary>
    /// <param name="multiLineStart">The text which starts a comment.</param>
    /// <param name="multiLineEnd">The text which ends a comment.</param>
    /// <param name="unterminated">What a comment without its end is: an error, not a comment, or a comment running to the
    /// end of the text.</param>
    public CommentsBuilder WithMultiLine(string multiLineStart, string multiLineEnd, UnterminatedComment unterminated)
    {
        ThrowHelper.ThrowIfNull(multiLineStart, nameof(multiLineStart));
        ThrowHelper.ThrowIfNull(multiLineEnd, nameof(multiLineEnd));

        if (unterminated is not (UnterminatedComment.Throw or UnterminatedComment.NotAComment or UnterminatedComment.ToEnd))
        {
            throw new ArgumentOutOfRangeException(nameof(unterminated));
        }

        _parsers.Add(unterminated == UnterminatedComment.Throw
            ? Literals.Comments(multiLineStart, multiLineEnd)
            : Capture(Literals.Text(multiLineStart).And(AnyCharBefore(Literals.Text(multiLineEnd), canBeEmpty: true, failOnEof: unterminated == UnterminatedComment.NotAComment, consumeDelimiter: true))));
        AddComment(new CommentsParser.CommentSyntax(multiLineStart, multiLineEnd, lineTerminators: null, unterminated));
        return this;
    }

    private void AddComment(CommentsParser.CommentSyntax comment)
    {
        // CommentsParser skips white space before it looks for a comment: a comment which starts with a white space char,
        // or with nothing, is matched by the composed parsers in the order they were added
        if (comment.Start.Length == 0 || comment.End?.Length == 0 || Character.IsWhiteSpaceOrNewLine(comment.Start[0]))
        {
            _composed = true;
        }

        _comments.Add(comment);
    }

    /// <summary>
    /// Builds the parser skipping the white space and the comments, which fails when there is nothing to skip.
    /// </summary>
    /// <remarks>
    /// Without <see cref="WithParser(Parser{TextSpan})"/> it is a <see cref="CommentsParser"/>, which skips everything in a
    /// single loop. Otherwise it is the choice of the parsers of the elements, in a loop.
    /// </remarks>
    public Parser<TextSpan> Build()
    {
        if (_composed || _parsers.Count == 0)
        {
            return Capture(OneOrMany(OneOf(_parsers.ToArray())));
        }

        return new CommentsParser(_whiteSpace || _whiteSpaceOrNewLine, _whiteSpaceOrNewLine, _comments.ToArray());
    }
}
