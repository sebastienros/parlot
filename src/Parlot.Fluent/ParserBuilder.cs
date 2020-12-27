using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public static class ParserBuilder
    {
        /// <summary>
        /// Provide parsers for literals.
        /// Literals do not skip spaces before being parsed and can be combined to
        /// parse composite terms.
        /// </summary>
        public static LiteralBuilder Literals => new();

        /// <summary>
        /// Provide parsers for terms.
        /// Terms skip spaces before being parsed.
        /// </summary>
        public static TermBuilder Terms => new();

        public static Then<T, U> Then<T, U>(this IParser<T> parser, Func<T, U> conversion) => new(parser, conversion);
        public static When<T> When<T>(this IParser<T> parser, Func<T, bool> predicate) => new(parser, predicate);

        public static OneOf OneOf(params IParser[] parsers) => new(parsers);
        public static OneOf<T> OneOf<T>(params IParser<T>[] parsers) => new(parsers);
        public static Sequence Sequence(params IParser[] parsers) => new(parsers);

        public static Sequence<T1, T2> Sequence<T1, T2>(IParser<T1> parser1, IParser<T2> parser2) => new (parser1, parser2);
        public static Sequence<T1, T2, T3> Sequence<T1, T2, T3>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3) => new(parser1, parser2, parser3);
        public static Sequence<T1, T2, T3, T4> Sequence<T1, T2, T3, T4>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4) => new(parser1, parser2, parser3, parser4);
        public static Sequence<T1, T2, T3, T4, T5> Sequence<T1, T2, T3, T4, T5>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4, IParser<T5> parser5) => new(parser1, parser2, parser3, parser4, parser5);
        public static Sequence<T1, T2, T3, T4, T5, T6> Sequence<T1, T2, T3, T4, T5, T6>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4, IParser<T5> parser5, IParser<T6> parser6) => new(parser1, parser2, parser3, parser4, parser5, parser6);
        public static Sequence<T1, T2, T3, T4, T5, T6, T7> Sequence<T1, T2, T3, T4, T5, T6, T7>(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, IParser<T4> parser4, IParser<T5> parser5, IParser<T6> parser6, IParser<T7> parser7) => new(parser1, parser2, parser3, parser4, parser5, parser6, parser7);

        public static ZeroOrOne<T> ZeroOrOne<T>(IParser<T> parser) => new(parser);
        public static ZeroOrMany<T> ZeroOrMany<T>(IParser<T> parser) => new(parser);
        public static OneOrMany<T> OneOrMany<T>(IParser<T> parser)
        {
            OneOrMany<T> oneOrMany = new(parser);
            return oneOrMany;
        }

        public static Deferred<T> Deferred<T>() => new();
        public static Between<T> Between<T>(string before, IParser<T> parser, string after) => new(before, parser, after);
    }

    public class LiteralBuilder
    {
        public TextLiteral Text(string text, bool caseInsensitive = false) => new(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null, skipWhiteSpace: false);
        public CharLiteral Char(char c) => new(c, false);
        public IntegerLiteral Integer() => new(false);
        public DecimalLiteral Decimal() => new(false);
        public StringLiteral String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new(quotes, false);
    }

    public class TermBuilder
    {
        public TextLiteral Text(string text, bool caseInsensitive = false) => new(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null);
        public CharLiteral Char(char c) => new(c);
        public IntegerLiteral Integer() => new();
        public DecimalLiteral Decimal() => new();
        public StringLiteral String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new(quotes);
    }
}
