﻿using System;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2>> And<T1, T2>(this Parser<T1> parser, Parser<T2> and) => new Sequence<T1, T2>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3>> And<T1, T2, T3>(this Parser<ValueTuple<T1, T2>> parser, Parser<T3> and) => new Sequence<T1, T2, T3>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4>> And<T1, T2, T3, T4>(this Parser<ValueTuple<T1, T2, T3>> parser, Parser<T4> and) => new Sequence<T1, T2, T3, T4>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5>> And<T1, T2, T3, T4, T5>(this Parser<ValueTuple<T1, T2, T3, T4>> parser, Parser<T5> and) => new Sequence<T1, T2, T3, T4, T5>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> And<T1, T2, T3, T4, T5, T6>(this Parser<ValueTuple<T1, T2, T3, T4, T5>> parser, Parser<T6> and) => new Sequence<T1, T2, T3, T4, T5, T6>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> And<T1, T2, T3, T4, T5, T6, T7>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> parser, Parser<T7> and) => new Sequence<T1, T2, T3, T4, T5, T6, T7>(parser, and);
    }
}
