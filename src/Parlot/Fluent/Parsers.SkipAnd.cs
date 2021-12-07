using System;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<T2, TParseContext> SkipAnd<T1, T2, TParseContext>(this Parser<T1, TParseContext> parser, Parser<T2, TParseContext> and) where TParseContext : ParseContext => new SequenceSkipAnd<T1, T2, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T3>, TParseContext> SkipAnd<T1, T2, T3, TParseContext>(this Parser<ValueTuple<T1, T2>, TParseContext> parser, Parser<T3, TParseContext> and) where TParseContext : ParseContext => new SequenceSkipAnd<T1, T2, T3, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T4>, TParseContext> SkipAnd<T1, T2, T3, T4, TParseContext>(this Parser<ValueTuple<T1, T2, T3>, TParseContext> parser, Parser<T4, TParseContext> and) where TParseContext : ParseContext => new SequenceSkipAnd<T1, T2, T3, T4, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T5>, TParseContext> SkipAnd<T1, T2, T3, T4, T5, TParseContext>(this Parser<ValueTuple<T1, T2, T3, T4>, TParseContext> parser, Parser<T5, TParseContext> and) where TParseContext : ParseContext => new SequenceSkipAnd<T1, T2, T3, T4, T5, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T6>, TParseContext> SkipAnd<T1, T2, T3, T4, T5, T6, TParseContext>(this Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext> parser, Parser<T6, TParseContext> and) where TParseContext : ParseContext => new SequenceSkipAnd<T1, T2, T3, T4, T5, T6, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T7>, TParseContext> SkipAnd<T1, T2, T3, T4, T5, T6, T7, TParseContext>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext> parser, Parser<T7, TParseContext> and) where TParseContext : ParseContext => new SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T8>, TParseContext> SkipAnd<T1, T2, T3, T4, T5, T6, T7, T8, TParseContext>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext> parser, Parser<T8, TParseContext> and) where TParseContext : ParseContext => new SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7, T8, TParseContext>(parser, and);
    }
}
