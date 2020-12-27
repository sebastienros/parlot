using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public static partial class Parsers
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

        // TODO: Decide between Bang and ZeroOrOne
        public static ZeroOrOne<T> Bang<T>(IParser<T> parser) => new(parser);
        public static ZeroOrOne<T> ZeroOrOne<T>(IParser<T> parser) => new(parser);

        // TODO: Decide between Star and ZeroOrMany
        public static ZeroOrMany<T> Star<T>(IParser<T> parser) => new (parser);
        public static ZeroOrMany<T> ZeroOrMany<T>(IParser<T> parser) => new (parser);

        // TODO: Decide between Plus and OneOrMany
        public static OneOrMany<T> Plus<T>(IParser<T> parser) => new (parser);
        public static OneOrMany<T> OneOrMany<T>(IParser<T> parser) => new (parser);

        public static Deferred<T> Deferred<T>() => new ();
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
