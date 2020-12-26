using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public static class ParserBuilder
    {
        public static LiteralBuilder Literals => new();

        public static IParser<U> Then<T, U>(this IParser<T> parser, Func<T, U> conversion) => new Then<T, U>(parser, conversion);
        public static IParser<decimal> AsDecimal(this IParser<TokenResult> parser) => new DecimalConversion(parser);

        public static IParser<ParseResult<object>> OneOf(params IParser[] parsers) => new OneOf(parsers);
        public static IParser<T> OneOf<T>(params IParser<T>[] parsers) => new OneOf<T>(parsers);
        public static IParser<IList<ParseResult<object>>> Sequence(params IParser[] parsers) => new Sequence(parsers);

        public static IParser<Tuple<T1, T2>> Sequence<T1, T2>(IParser<T1> parser1, IParser<T2> parser2) => new Sequence<T1, T2>(parser1, parser2);
        public static IParser<Tuple<T1, T2, T3>> Sequence<T1, T2, T3>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3) => new Sequence<T1, T2, T3>(parser1, parser2, parser3);

        public static IParser<T> ZeroOrOne<T>(IParser<T> parser) => new ZeroOrOne<T>(parser);
        public static IParser<IList<T>> ZeroOrMany<T>(IParser<T> parser) => new ZeroOrMany<T>(parser);
        public static IParser<IList<T>> OneOrMany<T>(IParser<T> parser) => new OneOrMany<T>(parser);

        public static Lazy<T> Lazy<T>() => new();
    }

    public class LiteralBuilder
    {
        public IParser<string> String(string text) => new StringLiteral(text);
        public IParser<char> Char(char c) => new CharLiteral(c);
        public IParser<TokenResult> Number() => new NumberLiteral();
    }
}
