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

        public static IParser<IList<T>> Separated<T>(IParser separator, IParser<T> parser) => new Separated<T>(separator, parser);

        // TODO: Decide between Bang and ZeroOrOne
        public static IParser<T> Bang<T>(IParser<T> parser) => new ZeroOrOne<T>(parser);
        public static IParser<T> ZeroOrOne<T>(IParser<T> parser) => new ZeroOrOne<T>(parser);

        // TODO: Decide between Star and ZeroOrMany
        public static IParser<IList<T>> Star<T>(IParser<T> parser) => new ZeroOrMany<T>(parser);
        public static IParser<IList<T>> ZeroOrMany<T>(IParser<T> parser) => new ZeroOrMany<T>(parser);

        // TODO: Decide between Plus and OneOrMany
        public static IParser<IList<T>> Plus<T>(IParser<T> parser) => new OneOrMany<T>(parser);
        public static IParser<IList<T>> OneOrMany<T>(IParser<T> parser) => new OneOrMany<T>(parser);

        public static Deferred<T> Deferred<T>() => new ();
        public static IParser<T> Between<T>(IParser before, IParser<T> parser, IParser after) => new Between<T>(before, parser, after);
    }

    public class LiteralBuilder
    {
        public IParser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null, skipWhiteSpace: false);
        public IParser<char> Char(char c) => new CharLiteral(c, skipWhiteSpace: false);
        public IParser<long> Integer() => new IntegerLiteral(skipWhiteSpace: false);
        public IParser<decimal> Decimal() => new DecimalLiteral(skipWhiteSpace: false);
        public IParser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes, skipWhiteSpace: false);
    }

    public class TermBuilder
    {
        public IParser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null);
        public IParser<char> Char(char c) => new CharLiteral(c);
        public IParser<long> Integer() => new IntegerLiteral();
        public IParser<decimal> Decimal() => new DecimalLiteral();
        public IParser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes);
    }
}
