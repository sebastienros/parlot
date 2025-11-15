
using Parlot.Fluent;
using System.Runtime.CompilerServices;

namespace Parlot;

// Other operators that could be overloaded but are not:
// & (bitwise and) could be used for AndAlso (logical and)
// ^ (bitwise xor) could be used for exclusive Or
// ~ (bitwise not) could be used for Not
// + (unary plus) could be used for something like "at least one"
// - (unary minus) could be used for something like "optional"
// ++ (increment) could be used for "one or more"
// -- (decrement) could be used for "zero or more"
// == (equality) could be used for "equals"
// != (inequality) could be used for "not equals"
// - (subtraction) could be used for "except"
// * (multiplication) could be used for "repeat n times"
// / (division) could be used for "divide into n parts"
// % (modulus) could be used for "repeat until condition met"
// << (left shift) could be used for "lookahead"
// >> (right shift) could be used for "lookbehind"
// >>> (unsigned right shift) could be used for "skip n characters"
// ! (logical not) could be used for "not"
// ? (ternary conditional) could be used for "if-then-else"
// >= (greater than or equal to) could be used for "at least n times"
// <= (less than or equal to) could be used for "at most n times"
// < (less than) could be used for "less than n times"
// > (greater than) could be used for "more than n times"

public static partial class ParserOperatorExtensions
{
    // + operator to replace And method

    extension<T1, T2>(Parser<T1>)
    {
        [OverloadResolutionPriority(-1)]
        public static Sequence<T1, T2> operator +(Parser<T1> p1, Parser<T2> p2)
        {
            return p1.And(p2);
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
            return new([new IParserAdapter<T>(p1), new IParserAdapter<T>(p2)]);
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
}
