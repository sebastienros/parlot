using System;
using System.Linq;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        public static Sequence And(this IParser parser, IParser and)
        {
            if (parser is Sequence sequence)
            {
                // Return a single OneOf instance with this new one
                return new Sequence(sequence._parsers.Concat(new[] { and }).ToArray());
            }
            else
            {
                return new Sequence(new[] { parser, and });
            }
        }

        public static IParser<ValueTuple<T1, T2>> And<T1, T2>(this IParser<T1> parser, IParser<T2> and) => new Sequence<T1, T2>(parser, and);
        public static IParser<ValueTuple<T1, T2, T3>> And<T1, T2, T3>(this IParser<ValueTuple<T1, T2>> parser, IParser<T3> and) => new Sequence<T1, T2, T3>(parser, and);
        public static IParser<ValueTuple<T1, T2, T3, T4>> And<T1, T2, T3, T4>(this IParser<ValueTuple<T1, T2, T3>> parser, IParser<T4> and) => new Sequence<T1, T2, T3, T4>(parser, and);
        public static IParser<ValueTuple<T1, T2, T3, T4, T5>> And<T1, T2, T3, T4, T5>(this IParser<ValueTuple<T1, T2, T3, T4>> parser, IParser<T5> and) => new Sequence<T1, T2, T3, T4, T5>(parser, and);
        public static IParser<ValueTuple<T1, T2, T3, T4, T5, T6>> And<T1, T2, T3, T4, T5, T6>(this IParser<ValueTuple<T1, T2, T3, T4, T5>> parser, IParser<T6> and) => new Sequence<T1, T2, T3, T4, T5, T6>(parser, and);
        public static IParser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> And<T1, T2, T3, T4, T5, T6, T7>(this IParser<ValueTuple<T1, T2, T3, T4, T5, T6>> parser, IParser<T7> and) => new Sequence<T1, T2, T3, T4, T5, T6, T7>(parser, and);

        public static Sequence Sequence(params IParser[] parsers) => new(parsers);

        public static IParser<ValueTuple<T1, T2>> Sequence<T1, T2>(IParser<T1> parser1, IParser<T2> parser2) => new Sequence<T1, T2>(parser1, parser2);
        public static IParser<ValueTuple<T1, T2, T3>> Sequence<T1, T2, T3>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3) => parser1.And(parser2).And(parser3);
        public static IParser<ValueTuple<T1, T2, T3, T4>> Sequence<T1, T2, T3, T4>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4) => parser1.And(parser2).And(parser3).And(parser4);
        public static IParser<ValueTuple<T1, T2, T3, T4, T5>> Sequence<T1, T2, T3, T4, T5>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4, IParser<T5> parser5) => parser1.And(parser2).And(parser3).And(parser4).And(parser5);
        public static IParser<ValueTuple<T1, T2, T3, T4, T5, T6>> Sequence<T1, T2, T3, T4, T5, T6>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4, IParser<T5> parser5, IParser<T6> parser6) => parser1.And(parser2).And(parser3).And(parser4).And(parser5).And(parser6);
        public static IParser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> Sequence<T1, T2, T3, T4, T5, T6, T7>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4, IParser<T5> parser5, IParser<T6> parser6, IParser<T7> parser7) => parser1.And(parser2).And(parser3).And(parser4).And(parser5).And(parser6).And(parser7);
    }
}
