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

        public static Sequence<T1, T2> And<T1, T2>(this IParser<T1> parser, IParser<T2> and) => new(parser, and);
        public static Sequence<T1, T2, T3> And<T1, T2, T3>(this Sequence<T1, T2> parser, IParser<T3> and) => new(parser._parser1, parser._parser2, and);
        public static Sequence<T1, T2, T3, T4> And<T1, T2, T3, T4>(this Sequence<T1, T2, T3> parser, IParser<T4> and) => new(parser._parser1, parser._parser2, parser._parser3, and);
        public static Sequence<T1, T2, T3, T4, T5> And<T1, T2, T3, T4, T5>(this Sequence<T1, T2, T3, T4> parser, IParser<T5> and) => new(parser._parser1, parser._parser2, parser._parser3, parser._parser4, and);
        public static Sequence<T1, T2, T3, T4, T5, T6> And<T1, T2, T3, T4, T5, T6>(this Sequence<T1, T2, T3, T4, T5> parser, IParser<T6> and) => new(parser._parser1, parser._parser2, parser._parser3, parser._parser4, parser._parser5, and);
        public static Sequence<T1, T2, T3, T4, T5, T6, T7> And<T1, T2, T3, T4, T5, T6, T7>(this Sequence<T1, T2, T3, T4, T5, T6> parser, IParser<T7> and) => new(parser._parser1, parser._parser2, parser._parser3, parser._parser4, parser._parser5, parser._parser6, and);

        public static Sequence Sequence(params IParser[] parsers) => new(parsers);

        public static Sequence<T1, T2> Sequence<T1, T2>(IParser<T1> parser1, IParser<T2> parser2) => new (parser1, parser2);
        public static Sequence<T1, T2, T3> Sequence<T1, T2, T3>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3) => new(parser1, parser2, parser3);
        public static Sequence<T1, T2, T3, T4> Sequence<T1, T2, T3, T4>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4) => new(parser1, parser2, parser3, parser4);
        public static Sequence<T1, T2, T3, T4, T5> Sequence<T1, T2, T3, T4, T5>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4, IParser<T5> parser5) => new(parser1, parser2, parser3, parser4, parser5);
        public static Sequence<T1, T2, T3, T4, T5, T6> Sequence<T1, T2, T3, T4, T5, T6>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4, IParser<T5> parser5, IParser<T6> parser6) => new(parser1, parser2, parser3, parser4, parser5, parser6);
        public static Sequence<T1, T2, T3, T4, T5, T6, T7> Sequence<T1, T2, T3, T4, T5, T6, T7>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4, IParser<T5> parser5, IParser<T6> parser6, IParser<T7> parser7) => new(parser1, parser2, parser3, parser4, parser5, parser6, parser7);
    }
}
