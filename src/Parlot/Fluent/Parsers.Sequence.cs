using System;
using System.Linq;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        public static Parser<ValueTuple<T1, T2>> And<T1, T2>(this Parser<T1> parser, Parser<T2> and) => new Sequence<T1, T2>(parser, and);
        public static Parser<ValueTuple<T1, T2, T3>> And<T1, T2, T3>(this Parser<ValueTuple<T1, T2>> parser, Parser<T3> and) => new Sequence<T1, T2, T3>(parser, and);
        public static Parser<ValueTuple<T1, T2, T3, T4>> And<T1, T2, T3, T4>(this Parser<ValueTuple<T1, T2, T3>> parser, Parser<T4> and) => new Sequence<T1, T2, T3, T4>(parser, and);
        public static Parser<ValueTuple<T1, T2, T3, T4, T5>> And<T1, T2, T3, T4, T5>(this Parser<ValueTuple<T1, T2, T3, T4>> parser, Parser<T5> and) => new Sequence<T1, T2, T3, T4, T5>(parser, and);
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> And<T1, T2, T3, T4, T5, T6>(this Parser<ValueTuple<T1, T2, T3, T4, T5>> parser, Parser<T6> and) => new Sequence<T1, T2, T3, T4, T5, T6>(parser, and);
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> And<T1, T2, T3, T4, T5, T6, T7>(this Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> parser, Parser<T7> and) => new Sequence<T1, T2, T3, T4, T5, T6, T7>(parser, and);

        public static Parser<ValueTuple<T1, T2>> Sequence<T1, T2>(Parser<T1> parser1, Parser<T2> parser2) => new Sequence<T1, T2>(parser1, parser2);
        public static Parser<ValueTuple<T1, T2, T3>> Sequence<T1, T2, T3>(Parser<T1> parser1, Parser<T2> parser2, Parser<T3> parser3) => parser1.And(parser2).And(parser3);
        public static Parser<ValueTuple<T1, T2, T3, T4>> Sequence<T1, T2, T3, T4>(Parser<T1> parser1, Parser<T2> parser2, Parser<T3> parser3, Parser<T4> parser4) => parser1.And(parser2).And(parser3).And(parser4);
        public static Parser<ValueTuple<T1, T2, T3, T4, T5>> Sequence<T1, T2, T3, T4, T5>(Parser<T1> parser1, Parser<T2> parser2, Parser<T3> parser3, Parser<T4> parser4, Parser<T5> parser5) => parser1.And(parser2).And(parser3).And(parser4).And(parser5);
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> Sequence<T1, T2, T3, T4, T5, T6>(Parser<T1> parser1, Parser<T2> parser2, Parser<T3> parser3, Parser<T4> parser4, Parser<T5> parser5, Parser<T6> parser6) => parser1.And(parser2).And(parser3).And(parser4).And(parser5).And(parser6);
        public static Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> Sequence<T1, T2, T3, T4, T5, T6, T7>(Parser<T1> parser1, Parser<T2> parser2, Parser<T3> parser3, Parser<T4> parser4, Parser<T5> parser5, Parser<T6> parser6, Parser<T7> parser7) => parser1.And(parser2).And(parser3).And(parser4).And(parser5).And(parser6).And(parser7);
    }
}
