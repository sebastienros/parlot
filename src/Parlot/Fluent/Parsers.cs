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

        public static Parser<List<T>> Separated<U, T>(Parser<U> separator, Parser<T> parser) => new Separated<U, T>(separator, parser);

        // TODO: Decide between Bang and ZeroOrOne
        public static Parser<T> Bang<T>(Parser<T> parser) => new ZeroOrOne<T>(parser);
        public static Parser<T> ZeroOrOne<T>(Parser<T> parser) => new ZeroOrOne<T>(parser);

        // TODO: Decide between Star and ZeroOrMany
        public static Parser<List<T>> Star<T>(Parser<T> parser) => new ZeroOrMany<T>(parser);
        public static Parser<List<T>> ZeroOrMany<T>(Parser<T> parser) => new ZeroOrMany<T>(parser);

        // TODO: Decide between Plus and OneOrMany
        public static Parser<List<T>> Plus<T>(Parser<T> parser) => new OneOrMany<T>(parser);
        public static Parser<List<T>> OneOrMany<T>(Parser<T> parser) => new OneOrMany<T>(parser);

        public static Parser<T> Not<T>(Parser<T> parser) => new Not<T>(parser);
        public static Deferred<T> Deferred<T>() => new Deferred<T>();
        public static Deferred<T> Recursive<T>(Func<Deferred<T>, Parser<T>> parser) => new Deferred<T>(parser);
        public static Parser<T> Between<A, T, B>(Parser<A> before, Parser<T> parser, Parser<B> after) => new Between<A, T, B>(before, parser, after);
        public static Parser<TextSpan> AnyCharBefore<T>(Parser<T> parser, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false) => new TextBefore<T>(parser, canBeEmpty, failOnEof, consumeDelimiter);
        public static Parser<U> SkipAnd<T, U>(this Parser<T> parser, Parser<U> and) => new SkipAnd<T, U>(parser, and);
        public static Parser<T> AndSkip<T, U>(this Parser<T> parser, Parser<U> and) => new AndSkip<T, U>(parser, and);

    }

    public class LiteralBuilder
    {
        public Parser<TextSpan> WhiteSpace(bool includeNewLines = false) => new WhiteSpaceLiteral(includeNewLines);
        public Parser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null, skipWhiteSpace: false);
        public Parser<char> Char(char c) => new CharLiteral(c, skipWhiteSpace: false);
        public Parser<long> Integer() => new IntegerLiteral(skipWhiteSpace: false);
        public Parser<decimal> Decimal() => new DecimalLiteral(skipWhiteSpace: false);
        public Parser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes, skipWhiteSpace: false);
        public Parser<TextSpan> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier(extraStart, extraPart, skipWhiteSpace: false);
    }

    public class TermBuilder
    {
        public Parser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null);
        public Parser<char> Char(char c) => new CharLiteral(c);
        public Parser<long> Integer(NumberOptions numberOptions = NumberOptions.Default) => new IntegerLiteral(numberOptions);
        public Parser<decimal> Decimal(NumberOptions numberOptions = NumberOptions.Default) => new DecimalLiteral(numberOptions);
        public Parser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes);
        public Parser<TextSpan> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier(extraStart, extraPart);
    }
}
