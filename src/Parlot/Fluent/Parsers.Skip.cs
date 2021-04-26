namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        /// <summary>
        /// Ensure the specified parser follows the previous one. The previous parser's result is then ignored.
        /// </summary>
        public static Parser<U> SkipAnd<T, U>(this Parser<T> parser, Parser<U> and) => new SkipAnd<T, U>(parser, and);

        /// <summary>
        /// Ensure the specified parser follows the previous one. The next parser's result is then ignored.
        /// </summary>
        public static Parser<T> AndSkip<T, U>(this Parser<T> parser, Parser<U> and) => new AndSkip<T, U>(parser, and);
    }
}
