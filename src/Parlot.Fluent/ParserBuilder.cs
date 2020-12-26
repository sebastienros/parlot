using System;

namespace Parlot.Fluent
{
    public static class ParserBuilder
    {
        public static IParser<TokenResult> String(string text) => text?.Length == 1 ? new CharTerminal(text[0]) : new StringTerminal(text);
        public static IParser<TokenResult> Char(char c) => new CharTerminal(c);
        public static IParser<TokenResult> Number => new NumberTerminal();

        public static IParser<U> Then<T, U>(this IParser<T> parser, Func<T, U> conversion) => new Then<T, U>(parser, conversion);
        public static IParser<decimal> AsDecimal(this IParser<TokenResult> parser) => new DecimalConversion(parser);

        public static IParser<IParseResult<T>> FirstOf<T>(params IParser<T>[] parsers) => new FirstOf<T>(parsers);
        public static IParser<IParseResult<T>[]> Sequence<T>(params IParser<T>[] parsers) => new Sequence<T>(parsers);
    }
}
