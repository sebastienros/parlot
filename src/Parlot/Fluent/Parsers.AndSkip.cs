namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static SequenceAndSkip<T1, T2> AndSkip<T1, T2>(this Parser<T1> parser, Parser<T2> and) => new(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static SequenceAndSkip<T1, T2, T3> AndSkip<T1, T2, T3>(this Sequence<T1, T2> parser, Parser<T3> and) => new(parser, and);
        public static SequenceAndSkip<T1, T3> AndSkip<T1, T2, T3>(this SequenceAndSkip<T1, T2> parser, Parser<T3> and) => new(parser, and);
        public static SequenceAndSkip<T2, T3> AndSkip<T1, T2, T3>(this SequenceSkipAnd<T1, T2> parser, Parser<T3> and) => new(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static SequenceAndSkip<T1, T2, T3, T4> AndSkip<T1, T2, T3, T4>(this Sequence<T1, T2, T3> parser, Parser<T4> and) => new(parser, and);
        public static SequenceAndSkip<T1, T2, T4> AndSkip<T1, T2, T3, T4>(this SequenceAndSkip<T1, T2, T3> parser, Parser<T4> and) => new(parser, and);
        public static SequenceAndSkip<T1, T3, T4> AndSkip<T1, T2, T3, T4>(this SequenceSkipAnd<T1, T2, T3> parser, Parser<T4> and) => new(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static SequenceAndSkip<T1, T2, T3, T4, T5> AndSkip<T1, T2, T3, T4, T5>(this Sequence<T1, T2, T3, T4> parser, Parser<T5> and) => new(parser, and);
        public static SequenceAndSkip<T1, T2, T3, T5> AndSkip<T1, T2, T3, T4, T5>(this SequenceAndSkip<T1, T2, T3, T4> parser, Parser<T5> and) => new(parser, and);
        public static SequenceAndSkip<T1, T2, T4, T5> AndSkip<T1, T2, T3, T4, T5>(this SequenceSkipAnd<T1, T2, T3, T4> parser, Parser<T5> and) => new(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static SequenceAndSkip<T1, T2, T3, T4, T5, T6> AndSkip<T1, T2, T3, T4, T5, T6>(this Sequence<T1, T2, T3, T4, T5> parser, Parser<T6> and) => new(parser, and);
        public static SequenceAndSkip<T1, T2, T3, T4, T6> AndSkip<T1, T2, T3, T4, T5, T6>(this SequenceAndSkip<T1, T2, T3, T4, T5> parser, Parser<T6> and) => new(parser, and);
        public static SequenceAndSkip<T1, T2, T3, T5, T6> AndSkip<T1, T2, T3, T4, T5, T6>(this SequenceSkipAnd<T1, T2, T3, T4, T5> parser, Parser<T6> and) => new(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7> AndSkip<T1, T2, T3, T4, T5, T6, T7>(this Sequence<T1, T2, T3, T4, T5, T6> parser, Parser<T7> and) => new(parser, and);
        public static SequenceAndSkip<T1, T2, T3, T4, T5, T7> AndSkip<T1, T2, T3, T4, T5, T6, T7>(this SequenceAndSkip<T1, T2, T3, T4, T5, T6> parser, Parser<T7> and) => new(parser, and);
        public static SequenceAndSkip<T1, T2, T3, T4, T6, T7> AndSkip<T1, T2, T3, T4, T5, T6, T7>(this SequenceSkipAnd<T1, T2, T3, T4, T5, T6> parser, Parser<T7> and) => new(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
        /// </summary>
        public static SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7, T8> AndSkip<T1, T2, T3, T4, T5, T6, T7, T8>(this Sequence<T1, T2, T3, T4, T5, T6, T7> parser, Parser<T8> and) => new(parser, and);
        public static SequenceAndSkip<T1, T2, T3, T4, T5, T6, T8> AndSkip<T1, T2, T3, T4, T5, T6, T7, T8>(this SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7> parser, Parser<T8> and) => new(parser, and);
        public static SequenceAndSkip<T1, T2, T3, T4, T5, T7, T8> AndSkip<T1, T2, T3, T4, T5, T6, T7, T8>(this SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7> parser, Parser<T8> and) => new(parser, and);
    }
}
