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

        public static IParser<List<T>> Separated<U, T>(IParser<U> separator, IParser<T> parser) => new Separated<U, T>(separator, parser);

        // TODO: Decide between Bang and ZeroOrOne
        public static IParser<T> Bang<T>(IParser<T> parser) => new ZeroOrOne<T>(parser);
        public static IParser<T> ZeroOrOne<T>(IParser<T> parser) => new ZeroOrOne<T>(parser);

        // TODO: Decide between Star and ZeroOrMany
        public static IParser<List<T>> Star<T>(IParser<T> parser) => new ZeroOrMany<T>(parser);
        public static IParser<List<T>> ZeroOrMany<T>(IParser<T> parser) => new ZeroOrMany<T>(parser);

        // TODO: Decide between Plus and OneOrMany
        public static IParser<List<T>> Plus<T>(IParser<T> parser) => new OneOrMany<T>(parser);
        public static IParser<List<T>> OneOrMany<T>(IParser<T> parser) => new OneOrMany<T>(parser);

        public static IParser<T> Not<T>(IParser<T> parser) => new Not<T>(parser);
        public static IDeferredParser<T> Deferred<T>() => new Deferred<T>();
        public static IDeferredParser<T> Recursive<T>(Func<Deferred<T>, IParser<T>> parser) => new Deferred<T>(parser);
        public static IParser<T> Between<A, T, B>(IParser<A> before, IParser<T> parser, IParser<B> after) => new Between<A, T, B>(before, parser, after);
        public static IParser<TextSpan> AnyCharBefore<T>(IParser<T> parser, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false) => new TextBefore<T>(parser, canBeEmpty, failOnEof, consumeDelimiter);
        public static IParser<U> SkipAnd<T, U>(this IParser<T> parser, IParser<U> and) => new SkipAnd<T, U>(parser, and);
        public static IParser<T> AndSkip<T, U>(this IParser<T> parser, IParser<U> and) => new AndSkip<T, U>(parser, and);

    }

    public class LiteralBuilder
    {
        public IParser<TextSpan> WhiteSpace(bool includeNewLines = false) => new WhiteSpaceLiteral(includeNewLines);
        public IParser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null, skipWhiteSpace: false);
        public IParser<char> Char(char c) => new CharLiteral(c, skipWhiteSpace: false);
        public IParser<long> Integer() => new IntegerLiteral(skipWhiteSpace: false);
        public IParser<decimal> Decimal() => new DecimalLiteral(skipWhiteSpace: false);
        public IParser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes, skipWhiteSpace: false);
        public IParser<TextSpan> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier(extraStart, extraPart, skipWhiteSpace: false);
    }

    public class TermBuilder
    {
        public IParser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null);
        public IParser<char> Char(char c) => new CharLiteral(c);
        public IParser<long> Integer(NumberOptions numberOptions = NumberOptions.Default) => new IntegerLiteral(numberOptions);
        public IParser<decimal> Decimal(NumberOptions numberOptions = NumberOptions.Default) => new DecimalLiteral(numberOptions);
        public IParser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes);
        public IParser<TextSpan> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier(extraStart, extraPart);
    }
}
