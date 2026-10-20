
using Parlot.Fluent;
using System.Runtime.CompilerServices;

namespace Parlot;

// Operators that are deliberately not overloaded:
// & (bitwise and) reads like a sequence, which + already is
// * (multiplication) and / (division) have no repetition or partition parser to map to
// ^ (bitwise xor) has no exclusive choice parser to map to
// ++ and -- reassign their operand, so p++ cannot build a new parser
// == and != are reserved for reference equality
// >>> (unsigned right shift) and the comparison operators have no obvious parser meaning

public static partial class ParserOperatorExtensions
{
    // Binary operators on two parsers. They share one extension block: separate blocks with the same receiver
    // are reported by CA1708 on the .NET 10 SDK.

    extension<T1, T2>(Parser<T1>)
    {
        // + operator to replace And

        [OverloadResolutionPriority(-1)]
        public static Sequence<T1, T2> operator +(Parser<T1> p1, Parser<T2> p2)
        {
            return p1.And(p2);
        }

        // >> operator to replace SkipAnd: the result of the left parser is skipped, the arrow points to the kept one

        public static SequenceSkipAnd<T1, T2> operator >>(Parser<T1> p1, Parser<T2> p2)
        {
            return p1.SkipAnd(p2);
        }

        // << operator to replace AndSkip: the result of the right parser is skipped, the arrow points to the kept one

        public static SequenceAndSkip<T1, T2> operator <<(Parser<T1> p1, Parser<T2> p2)
        {
            return p1.AndSkip(p2);
        }

        // - operator to replace Not(b).SkipAnd(a): a matches unless b matches at the same position

        public static SequenceSkipAnd<T2, T1> operator -(Parser<T1> p1, Parser<T2> p2)
        {
            return Parsers.Not(p2).SkipAnd(p1);
        }

        // % operator to replace Separated: a list of the left parser separated by the right one

        public static Separated<T2, T1> operator %(Parser<T1> p1, Parser<T2> p2)
        {
            return new Separated<T2, T1>(p2, p1);
        }
    }

    extension<T1, T2, T3>(Sequence<T1, T2>)
    {
        public static Sequence<T1, T2, T3> operator +(Sequence<T1, T2> p1, IParser<T3> p2)
        {
            return p2 is Parser<T3> parser?
              p1.And(parser) :
              p1.And(new IParserAdapter<T3>(p2));
        }
    }

    extension<T1, T2, T3, T4>(Sequence<T1, T2, T3>)
    {
        public static Sequence<T1, T2, T3, T4> operator +(Sequence<T1, T2, T3> p1, IParser<T4> p2)
        {
            return p2 is Parser<T4> parser?
              p1.And(parser) :
              p1.And(new IParserAdapter<T4>(p2));
        }
    }

    extension<T1, T2, T3, T4, T5>(Sequence<T1, T2, T3, T4>)
    {
        public static Sequence<T1, T2, T3, T4, T5> operator +(Sequence<T1, T2, T3, T4> p1, IParser<T5> p2)
        {
            return p2 is Parser<T5> parser?
              p1.And(parser) :
              p1.And(new IParserAdapter<T5>(p2));
        }
    }

    extension<T1, T2, T3, T4, T5, T6>(Sequence<T1, T2, T3, T4, T5>)
    {
        public static Sequence<T1, T2, T3, T4, T5, T6> operator +(Sequence<T1, T2, T3, T4, T5> p1, IParser<T6> p2)
        {
            return p2 is Parser<T6> parser?
              p1.And(parser) :
              p1.And(new IParserAdapter<T6>(p2));
        }
    }

    extension<T1, T2, T3, T4, T5, T6, T7>(Sequence<T1, T2, T3, T4, T5, T6>)
    {
        public static Sequence<T1, T2, T3, T4, T5, T6, T7> operator +(Sequence<T1, T2, T3, T4, T5, T6> p1, IParser<T7> p2)
        {
            return p2 is Parser<T7> parser?
              p1.And(parser) :
              p1.And(new IParserAdapter<T7>(p2));
        }
    }

    // | operator to replace Or method

    extension<T>(IParser<T>)
    {
        public static OneOf<T> operator |(IParser<T> p1, IParser<T> p2)
        {
            // Typed parsers are passed as they are, like the + operator does, such that OneOf can
            // build its lookup table from them and strip their SkipWhiteSpace wrappers. The adapter
            // is only needed for the covariant IParser<T> implementations.
            return new([
                p1 as Parser<T> ?? new IParserAdapter<T>(p1),
                p2 as Parser<T> ?? new IParserAdapter<T>(p2)]);
        }
    }

    extension<T>(OneOf<T>)
    {
        public static OneOf<T> operator |(OneOf<T> p1, IParser<T> p2)
        {
            return p2 is Parser<T> parser ?
              new OneOf<T>([.. p1.OriginalParsers, parser]) :
              new([.. p1.OriginalParsers, new IParserAdapter<T>(p2)]);
        }
    }

    // Unary operators to replace Not, Optional, OneOrMany and ZeroOrMany

    extension<T>(Parser<T>)
    {
        public static Not<T> operator !(Parser<T> p)
        {
            return new Not<T>(p);
        }

        public static Optional<T> operator -(Parser<T> p)
        {
            return new Optional<T>(p);
        }

        public static OneOrMany<T> operator +(Parser<T> p)
        {
            return new OneOrMany<T>(p);
        }

        public static ZeroOrMany<T> operator ~(Parser<T> p)
        {
            return new ZeroOrMany<T>(p);
        }
    }
}
