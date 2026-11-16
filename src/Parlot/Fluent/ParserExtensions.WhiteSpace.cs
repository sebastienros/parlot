using System;
using System.Collections.Generic;
using System.Linq;

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

    // The chars of WithWhiteSpace(whiteSpace, newLines), without duplicates: a char of both is white space
    private string? _spaceChars;
    private string? _newLineChars;

    // How many white space elements were added
    private int _whiteSpaceCount;

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
        _whiteSpaceCount++;
        return this;
    }

    /// <summary>
    /// Skips the white space and new lines of the context's <see cref="ParseContext.WhiteSpaceSet"/>.
    /// </summary>
    public CommentsBuilder WithWhiteSpaceOrNewLine()
    {
        _parsers.Add(Literals.WhiteSpace(includeNewLines: true));
        _whiteSpaceOrNewLine = true;
        _whiteSpaceCount++;
        return this;
    }

    /// <summary>
    /// Skips the chars of <paramref name="whiteSpace"/> and of <paramref name="newLines"/>, instead of the white space of the
    /// context's <see cref="ParseContext.WhiteSpaceSet"/>.
    /// </summary>
    /// <param name="whiteSpace">The chars which are white space, for instance <c>"\t\v\f \u00A0\uFEFF"</c> and the other
    /// <c>Zs</c> chars for JavaScript.</param>
    /// <param name="newLines">The chars which are new lines, for instance <c>"\n\r\u2028\u2029"</c> for JavaScript. They are
    /// skipped unless <see cref="ParseContext.UseNewLines"/> is set, as the new lines of a <see cref="WhiteSpaceSet"/>. It can be
    /// empty, to skip no new line.</param>
    /// <remarks>
    /// <para>
    /// This is for a grammar whose white space no <see cref="WhiteSpaceSet"/> describes, like JavaScript's, which contains
    /// U+FEFF and not U+0085. It costs the same as the white space of a set: the chars are tested with a table built once.
    /// </para>
    /// <para>
    /// The parsers telling whether a new line was skipped, <see cref="Parsers.NoNewLineBefore{T}(Parser{T}, string)"/> and
    /// <see cref="Parsers.NewLineAhead(string)"/>, take the same <paramref name="newLines"/>. The line of
    /// <see cref="Cursor.Position"/> still counts the <c>'\n'</c> chars only.
    /// </para>
    /// </remarks>
    public CommentsBuilder WithWhiteSpace(string whiteSpace, string newLines)
    {
        ThrowHelper.ThrowIfNull(whiteSpace, nameof(whiteSpace));
        ThrowHelper.ThrowIfNull(newLines, nameof(newLines));

        if (whiteSpace.Length == 0 && newLines.Length == 0)
        {
            throw new ArgumentException("At least one white space or new line char is required.", nameof(whiteSpace));
        }

        var spaces = new string(whiteSpace.Distinct().ToArray());
        var lines = new string(newLines.Distinct().Where(c => spaces.IndexOf(c) < 0).ToArray());

        // A CommentsParser without comments skips the chars when the elements are composed, as with UseNewLines
        _parsers.Add(new CommentsParser(spaces, lines, []));
        _spaceChars = spaces;
        _newLineChars = lines;
        _whiteSpaceCount++;
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
        => WithSingleLine(singleLineStart, lineTerminators, CommentPlacement.Anywhere);

    /// <summary>
    /// Skips single line comments, from <paramref name="singleLineStart"/> to the first of <paramref name="lineTerminators"/>,
    /// which is not part of the comment, or to the end of the text, which start where <paramref name="placement"/> says.
    /// </summary>
    /// <param name="singleLineStart">The text which starts a comment.</param>
    /// <param name="lineTerminators">The chars which end a comment, for instance <c>"\r\n\u2028\u2029"</c> for JavaScript or C#.</param>
    /// <param name="placement">Where the comment starts. With <see cref="CommentPlacement.LineStart"/>, it only starts when
    /// the white space and comments skipped before it contain a new line, or when the skip started at the start of the input or
    /// after a new line:
    /// a new line of the white space, like the one which ends a single line comment, or a multi line comment containing a new
    /// line (see <see cref="WithMultiLine(string, string, UnterminatedComment, string)"/>).</param>
    /// <remarks>
    /// <para>
    /// JavaScript scripts (ECMAScript Annex B.1.1) skip <c>&lt;!--</c> anywhere and <c>--&gt;</c> at the start of a line:
    /// <c>WithSingleLine("&lt;!--", lineTerminators).WithSingleLine("--&gt;", lineTerminators, CommentPlacement.LineStart)</c>.
    /// </para>
    /// <para>
    /// A comment at the start of a line needs the builder to skip everything in a single <see cref="CommentsParser"/>:
    /// <see cref="Build"/> throws an <see cref="InvalidOperationException"/> when the builder composes the parsers of its
    /// elements instead.
    /// </para>
    /// </remarks>
    public CommentsBuilder WithSingleLine(string singleLineStart, string lineTerminators, CommentPlacement placement)
    {
        ThrowHelper.ThrowIfNull(singleLineStart, nameof(singleLineStart));
        ThrowHelper.ThrowIfNull(lineTerminators, nameof(lineTerminators));

        if (lineTerminators.Length == 0)
        {
            throw new ArgumentException("At least one line terminator is required.", nameof(lineTerminators));
        }

        if (placement is not (CommentPlacement.Anywhere or CommentPlacement.LineStart))
        {
            throw new ArgumentOutOfRangeException(nameof(placement));
        }

        _parsers.Add(Capture(Literals.Text(singleLineStart).And(AnyCharBefore(Literals.AnyOf(lineTerminators, maxSize: 1), canBeEmpty: true, failOnEof: false, consumeDelimiter: false))));
        AddComment(new CommentsParser.CommentSyntax(singleLineStart, end: null, lineTerminators, placement: placement));
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
        => AddMultiLine(multiLineStart, multiLineEnd, unterminated, lineTerminators: null, hasLineTerminators: false);

    /// <summary>
    /// Skips multi line comments, from <paramref name="multiLineStart"/> to <paramref name="multiLineEnd"/> included, which
    /// contain a new line when they contain one of <paramref name="lineTerminators"/>.
    /// </summary>
    /// <param name="multiLineStart">The text which starts a comment.</param>
    /// <param name="multiLineEnd">The text which ends a comment.</param>
    /// <param name="unterminated">What a comment without its end is: an error, not a comment, or a comment running to the
    /// end of the text.</param>
    /// <param name="lineTerminators">The chars of the new lines a comment can contain, for instance <c>"\n\r\u2028\u2029"</c>
    /// for JavaScript. A comment of the other overloads contains a new line when it contains one of the new lines of the
    /// white space: the chars of <c>newLines</c> with <see cref="WithWhiteSpace(string, string)"/>, which can be none, or
    /// the new lines of the context's <see cref="WhiteSpaceSet"/> otherwise.</param>
    /// <remarks>
    /// A comment containing a new line starts a line, where the comments of
    /// <see cref="WithSingleLine(string, string, CommentPlacement)"/> with <see cref="CommentPlacement.LineStart"/> can start.
    /// </remarks>
    public CommentsBuilder WithMultiLine(string multiLineStart, string multiLineEnd, UnterminatedComment unterminated, string lineTerminators)
        => AddMultiLine(multiLineStart, multiLineEnd, unterminated, lineTerminators, hasLineTerminators: true);

    private CommentsBuilder AddMultiLine(string multiLineStart, string multiLineEnd, UnterminatedComment unterminated, string? lineTerminators, bool hasLineTerminators)
    {
        ThrowHelper.ThrowIfNull(multiLineStart, nameof(multiLineStart));
        ThrowHelper.ThrowIfNull(multiLineEnd, nameof(multiLineEnd));

        if (hasLineTerminators)
        {
            ThrowHelper.ThrowIfNull(lineTerminators, nameof(lineTerminators));

            if (lineTerminators!.Length == 0)
            {
                throw new ArgumentException("At least one line terminator is required.", nameof(lineTerminators));
            }
        }

        if (unterminated is not (UnterminatedComment.Throw or UnterminatedComment.NotAComment or UnterminatedComment.ToEnd))
        {
            throw new ArgumentOutOfRangeException(nameof(unterminated));
        }

        var comment = new CommentsParser.CommentSyntax(multiLineStart, multiLineEnd, lineTerminators, unterminated);

        _parsers.Add(ComposedMultiLine(comment));
        AddComment(comment);
        return this;
    }

    // The parser of a multi line comment, for a builder which composes the parsers of its elements
    private static Parser<TextSpan> ComposedMultiLine(CommentsParser.CommentSyntax comment)
    {
        var start = comment.Start;
        var end = comment.End!;

        if (comment.Unterminated == UnterminatedComment.Throw)
        {
            return Literals.Comments(start, end);
        }

        // A CommentsParser of this comment alone, without white space, reads an unterminated one the same way in both
        // builders. It needs the first char of the start, and an end to search for
        if (start.Length > 0 && end.Length > 0)
        {
            return new CommentsParser(skipWhiteSpace: false, includeNewLines: false, [comment]);
        }

        return Capture(Literals.Text(start).And(AnyCharBefore(Literals.Text(end), canBeEmpty: true, failOnEof: comment.Unterminated == UnterminatedComment.NotAComment, consumeDelimiter: true)));
    }

    private void AddComment(CommentsParser.CommentSyntax comment)
    {
        // CommentsParser needs the first char of a comment, and an end to search for
        if (comment.Start.Length == 0 || comment.End?.Length == 0)
        {
            _composed = true;
        }

        _comments.Add(comment);
    }

    // Whether CommentsParser can skip the elements. It skips the white space of the context's set, with new lines when an
    // element includes them, or the chars of a single WithWhiteSpace(whiteSpace, newLines), before it looks for a comment: a
    // comment which starts with a white space char is matched by the composed parsers in the order they were added
    private bool CanSkipInOneParser()
    {
        if (_composed || _spaceChars is not null && _whiteSpaceCount > 1)
        {
            return false;
        }

        foreach (var comment in _comments)
        {
            var first = comment.Start[0];

            // The context's set isn't known when the grammar is built: the default set contains the chars of every set
            if (_spaceChars is not null ? (_spaceChars + _newLineChars).AsSpan().IndexOf(first) >= 0 : Character.IsWhiteSpaceOrNewLine(first))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds the parser skipping the white space and the comments, which fails when there is nothing to skip.
    /// </summary>
    /// <remarks>
    /// Without <see cref="WithParser(Parser{TextSpan})"/> it is a <see cref="CommentsParser"/>, which skips everything in a
    /// single loop. Otherwise it is the choice of the parsers of the elements, in a loop.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The builder has a comment of <see cref="CommentPlacement.LineStart"/>,
    /// which needs the single loop of <see cref="CommentsParser"/>, and composes the parsers of its elements
    /// instead.</exception>
    public Parser<TextSpan> Build()
    {
        if (_parsers.Count == 0 || !CanSkipInOneParser())
        {
            // The composed parsers can't tell where a line starts
            if (_comments.Exists(static c => c.Placement == CommentPlacement.LineStart))
            {
                throw new InvalidOperationException("A comment placed at the start of a line can't be skipped when the builder composes the parsers of its elements: with a parser added with WithParser(), a comment whose start is empty or starts with white space, a multi line comment whose end is empty, or WithWhiteSpace(whiteSpace, newLines) with another white space element.");
            }

            return Capture(OneOrMany(OneOf(_parsers.ToArray())));
        }

        return _spaceChars is not null
            ? new CommentsParser(_spaceChars, _newLineChars!, _comments.ToArray())
            : new CommentsParser(_whiteSpace || _whiteSpaceOrNewLine, _whiteSpaceOrNewLine, _comments.ToArray());
    }
}
