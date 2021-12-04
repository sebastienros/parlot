using System;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2>, TParseContext, TChar> And<T1, T2, TParseContext, TChar>(this Parser<T1, TParseContext, TChar> parser, Parser<T2, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3>, TParseContext, TChar> And<T1, T2, T3, TParseContext, TChar>(this Parser<ValueTuple<T1, T2>, TParseContext, TChar> parser, Parser<T3, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4>, TParseContext, TChar> And<T1, T2, T3, T4, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3>, TParseContext, TChar> parser, Parser<T4, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext, TChar> And<T1, T2, T3, T4, T5, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4>, TParseContext, TChar> parser, Parser<T5, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, T5, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext, TChar> And<T1, T2, T3, T4, T5, T6, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext, TChar> parser, Parser<T6, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, T5, T6, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext, TChar> And<T1, T2, T3, T4, T5, T6, T7, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext, TChar> parser, Parser<T7, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, T5, T6, T7, TParseContext, TChar>(parser, and);




        public static Parser<ValueTuple<T1, T2>, TParseContext, TChar> And<T1, T2, TParseContext, TChar>(this Parser<T1, TParseContext, TChar> parser, Parser<T2, TParseContext> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3>, TParseContext, TChar> And<T1, T2, T3, TParseContext, TChar>(this Parser<ValueTuple<T1, T2>, TParseContext, TChar> parser, Parser<T3, TParseContext> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4>, TParseContext, TChar> And<T1, T2, T3, T4, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3>, TParseContext, TChar> parser, Parser<T4, TParseContext> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext, TChar> And<T1, T2, T3, T4, T5, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4>, TParseContext, TChar> parser, Parser<T5, TParseContext> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, T5, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext, TChar> And<T1, T2, T3, T4, T5, T6, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext, TChar> parser, Parser<T6, TParseContext> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, T5, T6, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext, TChar> And<T1, T2, T3, T4, T5, T6, T7, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext, TChar> parser, Parser<T7, TParseContext> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, T5, T6, T7, TParseContext, TChar>(parser, and);




        public static Parser<ValueTuple<T1, T2>, TParseContext, TChar> And<T1, T2, TParseContext, TChar>(this Parser<T1, TParseContext> parser, Parser<T2, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3>, TParseContext, TChar> And<T1, T2, T3, TParseContext, TChar>(this Parser<ValueTuple<T1, T2>, TParseContext> parser, Parser<T3, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4>, TParseContext, TChar> And<T1, T2, T3, T4, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3>, TParseContext> parser, Parser<T4, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext, TChar> And<T1, T2, T3, T4, T5, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4>, TParseContext> parser, Parser<T5, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, T5, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext, TChar> And<T1, T2, T3, T4, T5, T6, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext> parser, Parser<T6, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, T5, T6, TParseContext, TChar>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext, TChar> And<T1, T2, T3, T4, T5, T6, T7, TParseContext, TChar>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext> parser, Parser<T7, TParseContext, TChar> and) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => new Sequence<T1, T2, T3, T4, T5, T6, T7, TParseContext, TChar>(parser, and);

    }
}
