namespace Parlot.Fluent;

public static partial class ParserExtensions
{
    /// <summary>
    /// Builds a parser that temporarily sets a custom whitespace parser for the current parser.
    /// The whitespace parser will be reset after the parser completes.
    /// </summary>
    /// <typeparam name="T">The type of the parser result.</typeparam>
    /// <param name="parser">The parser to execute with the custom whitespace parser.</param>
    /// <param name="whiteSpaceParser">The custom whitespace parser to use.</param>
    /// <returns>A parser that uses the custom whitespace parser.</returns>
    public static Parser<T> WithWhiteSpaceParser<T>(this Parser<T> parser, Parser<TextSpan> whiteSpaceParser)
        => new WithWhiteSpaceParser<T>(parser, whiteSpaceParser);
}
