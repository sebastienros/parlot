﻿using System;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2>, TParseContext> And<T1, T2, TParseContext>(this Parser<T1, TParseContext> parser, Parser<T2, TParseContext> and) where TParseContext : ParseContext => new Sequence<T1, T2, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3>, TParseContext> And<T1, T2, T3, TParseContext>(this Parser<ValueTuple<T1, T2>, TParseContext> parser, Parser<T3, TParseContext> and) where TParseContext : ParseContext => new Sequence<T1, T2, T3, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4>, TParseContext> And<T1, T2, T3, T4, TParseContext>(this Parser<ValueTuple<T1, T2, T3>, TParseContext> parser, Parser<T4, TParseContext> and) where TParseContext : ParseContext => new Sequence<T1, T2, T3, T4, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext> And<T1, T2, T3, T4, T5, TParseContext>(this Parser<ValueTuple<T1, T2, T3, T4>, TParseContext> parser, Parser<T5, TParseContext> and) where TParseContext : ParseContext => new Sequence<T1, T2, T3, T4, T5, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext> And<T1, T2, T3, T4, T5, T6, TParseContext>(this Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext> parser, Parser<T6, TParseContext> and) where TParseContext : ParseContext => new Sequence<T1, T2, T3, T4, T5, T6, TParseContext>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext> And<T1, T2, T3, T4, T5, T6, T7, TParseContext>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext> parser, Parser<T7, TParseContext> and) where TParseContext : ParseContext => new Sequence<T1, T2, T3, T4, T5, T6, T7, TParseContext>(parser, and);
    }
}
