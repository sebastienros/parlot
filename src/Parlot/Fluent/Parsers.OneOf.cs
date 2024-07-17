namespace Parlot.Fluent
{
    // We don't care about the performance of these helpers since they are called only once 
    // during the parser tree creation

    public static partial class Parsers
    {
        /// <summary>
        /// Builds a parser that return either of the first successful of the specified parsers.
        /// </summary>
        public static Parser<T> Or<T>(this Parser<T> parser, Parser<T> or)
        {
            // We don't care about the performance of these helpers since they are called only once 
            // during the parser tree creation

            if (parser is OneOf<T> oneOf)
            {
                // Return a single OneOf instance with this new one
                return new OneOf<T>([.. oneOf.Parsers, or]);
            }
            else
            {
                return new OneOf<T>([parser, or]);
            }
        }

        /// <summary>
        /// Builds a parser that return either of the first successful of the specified parsers.
        /// </summary>
        public static Parser<T> Or<A, B, T>(this Parser<A> parser, Parser<B> or)
            where A : T
            where B : T => new OneOf<A, B, T>(parser, or);

        /// <summary>
        /// Builds a parser that return either of the first successful of the specified parsers.
        /// </summary>
        public static Parser<T> OneOf<T>(params Parser<T>[] parsers) => new OneOf<T>(parsers);
    }
}
