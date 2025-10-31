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
    /// Builds a parser that sets comments for the current parser.
    /// </summary>
    /// <typeparam name="T">The type of the parser result.</typeparam>
    /// <param name="parser">The parser to execute with the custom whitespace parser.</param>
    /// <param name="commentsBuilder">The action to configure the comments builder.</param>
    /// <returns>A parser that uses white spaces, new lines and comments.</returns>
    public static Parser<T> WithComments<T>(this Parser<T> parser, Action<CommentsBuilder> commentsBuilder)
    {
        var builder = new CommentsBuilder(Literals.WhiteSpace(includeNewLines: true));
        commentsBuilder(builder);
        return new WithWhiteSpaceParser<T>(parser, builder.Build());
    }
}

public class CommentsBuilder
{
    private readonly List<Parser<TextSpan>> _parsers = [];

    public CommentsBuilder(Parser<TextSpan> whiteSpaceParser)
    {
        _parsers.Add(whiteSpaceParser);
    }

    public Parser<TextSpan> WithSingleLine(string singleLineStart)
    {
        var parser = Literals.Comments(singleLineStart);
        _parsers.Add(parser);
        return parser;
    }

    public Parser<TextSpan> WithMultiLine(string multiLineStart, string multiLineEnd)
    {
        var parser = Literals.Comments(multiLineStart, multiLineEnd);
        _parsers.Add(parser);
        return parser;
    }

    public Parser<TextSpan> Build() 
    {
        return Capture(ZeroOrMany(OneOf(_parsers.ToArray())));
    }
}
