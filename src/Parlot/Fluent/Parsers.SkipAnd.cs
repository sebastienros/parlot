using System;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<T2, TParseContext, TChar> SkipAnd<T1, T2, TParseContext, TChar>(this Parser<T1, TParseContext, TChar> parser, Parser<T2, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new SequenceSkipAnd<T1, T2, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T3>, TParseContext, TChar> SkipAnd<T1, T2, T3, TParseContext, TChar>(this Parser<ValueTuple<T1, T2>, TParseContext, TChar> parser, Parser<T3, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new SequenceSkipAnd<T1, T2, T3, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T4>, TParseContext, TChar> SkipAnd<T1, T2, T3, T4, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3>, TParseContext, TChar> parser, Parser<T4, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new SequenceSkipAnd<T1, T2, T3, T4, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T5>, TParseContext, TChar> SkipAnd<T1, T2, T3, T4, T5, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4>, TParseContext, TChar> parser, Parser<T5, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new SequenceSkipAnd<T1, T2, T3, T4, T5, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T6>, TParseContext, TChar> SkipAnd<T1, T2, T3, T4, T5, T6, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext, TChar> parser, Parser<T6, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new SequenceSkipAnd<T1, T2, T3, T4, T5, T6, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T7>, TParseContext, TChar> SkipAnd<T1, T2, T3, T4, T5, T6, T7, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext, TChar> parser, Parser<T7, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T8>, TParseContext, TChar> SkipAnd<T1, T2, T3, T4, T5, T6, T7, T8, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext, TChar> parser, Parser<T8, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7, T8, TParseContext, TChar>(parser, and);
    }
}
