namespace Parlot.Fluent;

public static partial class ParserExtensions
{
    /// <summary>
    /// Builds a parser returning the text of <paramref name="parser"/> as a string taken from a pool, such that a value
    /// matched many times is allocated once.
    /// </summary>
    /// <param name="parser">The parser whose text is returned, like <c>Terms.Identifier()</c> or <c>Terms.String()</c>.</param>
    /// <returns>A parser returning pooled strings.</returns>
    /// <remarks>
    /// The pool is shared by all the parses of the returned parser and is bounded, see <see cref="PooledString"/>. It pays
    /// off when the strings are kept, like the identifiers of a syntax tree held in memory. A string literal also decodes
    /// its escape sequences into the pool.
    /// </remarks>
    public static Parser<string> ToPooledString(this Parser<TextSpan> parser) => new PooledString(parser);
}
