using System.Linq;

namespace Parlot.Fluent
{
    // We don't care about the performance of these helpers since they are called only once 
    // during the parser tree creation

    public static partial class Parsers
    {
        /// <summary>
        /// Builds a parser that return either of the first successful of the specified parsers.
        /// </summary>
        public static Parser<T, TParseContext> Or<T, TParseContext>(this IParser<T, TParseContext> parser, IParser<T, TParseContext> or)
        where TParseContext : ParseContext
        {
            // We don't care about the performance of these helpers since they are called only once 
            // during the parser tree creation

            if (parser is OneOf<T, TParseContext> oneOf)
            {
                // Return a single OneOf instance with this new one
                return new OneOf<T, TParseContext>(oneOf.Parsers.Append(or).ToArray());
            }
            else
            {
                return new OneOf<T, TParseContext>(new[] { parser, or });
            }
        }

        /// <summary>
        /// Builds a parser that return either of the first successful of the specified parsers.
        /// </summary>
        public static Parser<T, TParseContext> Or<A, B, T, TParseContext>(this IParser<A, TParseContext> parser, IParser<B, TParseContext> or)
            where A : T
            where B : T
            where TParseContext : ParseContext
        {
            return new OneOf<A, B, T, TParseContext>(parser, or);
        }
    }

    public static partial class Parsers<TParseContext>
    where TParseContext : ParseContext
    {

        /// <summary>
        /// Builds a parser that return either of the first successful of the specified parsers.
        /// </summary>
        public static Parser<T, TParseContext> OneOf<T>(params IParser<T, TParseContext>[] parsers) => new OneOf<T, TParseContext>(parsers);
    }
}
