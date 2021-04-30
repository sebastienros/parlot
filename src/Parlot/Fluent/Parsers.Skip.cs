namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        /// <summary>
        /// Ensure the specified parser follows the previous one. The previous parser's result is then ignored.
        /// </summary>
        public static Parser<U, TParseContext> SkipAnd<T, U, TParseContext>(this IParser<T, TParseContext> parser, IParser<U, TParseContext> and) where TParseContext : ParseContext => new SkipAnd<T, U, TParseContext>(parser, and);
        
        
        /// <summary>
        /// Ensure the specified parser follows the previous one. The next parser's result is then ignored.
        /// </summary>
        public static Parser<T, TParseContext> AndSkip<T, U, TParseContext>(this IParser<T, TParseContext> parser, IParser<U, TParseContext> and) where TParseContext : ParseContext => new AndSkip<T, U, TParseContext>(parser, and);

    }
}
