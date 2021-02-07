using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        /// <summary>
        /// Provides parsers for literals. Literals do not skip spaces before being parsed and can be combined to
        /// parse composite terms.
        /// </summary>
        public static LiteralBuilder Literals => new();

        /// <summary>
        /// Provides parsers for terms. Terms skip spaces before being parsed.
        /// </summary>
        public static TermBuilder Terms => new();

        /// <summary>
        /// Builds a parser that looks for zero or many times a parser separated by another one.
        /// </summary>
        public static Parser<List<T>> Separated<U, T>(Parser<U> separator, Parser<T> parser) => new Separated<U, T>(separator, parser);

        /// <summary>
        /// Builds a parser that looks for zero or one time the specified parser.
        /// </summary>
        public static Parser<T> ZeroOrOne<T>(Parser<T> parser) => new ZeroOrOne<T>(parser);

        /// <summary>
        /// Builds a parser that looks for zero or many times the specified parser.
        /// </summary>
        public static Parser<List<T>> ZeroOrMany<T>(Parser<T> parser) => new ZeroOrMany<T>(parser);

        /// <summary>
        /// Builds a parser that looks for one or many times the specified parser.
        /// </summary>
        public static Parser<List<T>> OneOrMany<T>(Parser<T> parser) => new OneOrMany<T>(parser);

        /// <summary>
        /// Builds a parser that succeed when the specified parser fails to match.
        /// </summary>
        public static Parser<T> Not<T>(Parser<T> parser) => new Not<T>(parser);

        /// <summary>
        /// Builds a parser that can be defined later one. Use it when a parser need to be declared before its rule can be set.
        /// </summary>
        public static Deferred<T> Deferred<T>() => new();

        /// <summary>
        /// Builds a parser than needs a reference to itself to be declared.
        /// </summary>
        public static Deferred<T> Recursive<T>(Func<Deferred<T>, Parser<T>> parser) => new(parser);

        /// <summary>
        /// Builds a parser that matches the specified parser between two other ones.
        /// </summary>
        public static Parser<T> Between<A, T, B>(Parser<A> before, Parser<T> parser, Parser<B> after) => new Between<A, T, B>(before, parser, after);

        /// <summary>
        /// Builds a parser that matches any chars before a specific parser.
        /// </summary>
        public static Parser<TextSpan> AnyCharBefore<T>(Parser<T> parser, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false) => new TextBefore<T>(parser, canBeEmpty, failOnEof, consumeDelimiter);
        
        /// <summary>
        /// Ensure the specified parser follows the previous one. The previous parser's result is then ignored.
        /// </summary>
        public static Parser<U> SkipAnd<T, U>(this Parser<T> parser, Parser<U> and) => new SkipAnd<T, U>(parser, and);

        /// <summary>
        /// Ensure the specified parser follows the previous one. The next parser's result is then ignored.
        /// </summary>
        public static Parser<T> AndSkip<T, U>(this Parser<T> parser, Parser<U> and) => new AndSkip<T, U>(parser, and);
    }

    public class LiteralBuilder
    {
        /// <summary>
        /// Builds a parser that matches whitespaces.
        /// </summary>
        public Parser<TextSpan> WhiteSpace(bool includeNewLines = false) => new WhiteSpaceLiteral(includeNewLines);

        /// <summary>
        /// Builds a parser that matches anything until whitespaces.
        /// </summary>
        public Parser<TextSpan> NonWhiteSpace(bool includeNewLines = false) => new NonWhiteSpaceLiteral(skipWhiteSpace: false, includeNewLines: includeNewLines);

        /// <summary>
        /// Builds a parser that matches the specified text.
        /// </summary>
        public Parser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null, skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public Parser<char> Char(char c) => new CharLiteral(c, skipWhiteSpace: false);
        
        /// <summary>
        /// Builds a parser that matches an integer.
        /// </summary>
        public Parser<long> Integer() => new IntegerLiteral(skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches a floating point number.
        /// </summary>
        public Parser<decimal> Decimal() => new DecimalLiteral(skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches an quoted string that can be escaped.
        /// </summary>
        public Parser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes, skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches an identifier.
        /// </summary>
        public Parser<TextSpan> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier(extraStart, extraPart, skipWhiteSpace: false);
    }

    public class TermBuilder
    {
        /// <summary>
        /// Builds a parser that matches anything until whitespaces.
        /// </summary>
        public Parser<TextSpan> NonWhiteSpace(bool includeNewLines = false) => new NonWhiteSpaceLiteral(includeNewLines: includeNewLines);

        /// <summary>
        /// Builds a parser that matches the specified text.
        /// </summary>
        public Parser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null);

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public Parser<char> Char(char c) => new CharLiteral(c);

        /// <summary>
        /// Builds a parser that matches an integer.
        /// </summary>
        public Parser<long> Integer(NumberOptions numberOptions = NumberOptions.Default) => new IntegerLiteral(numberOptions);

        /// <summary>
        /// Builds a parser that matches a floating point number.
        /// </summary>
        public Parser<decimal> Decimal(NumberOptions numberOptions = NumberOptions.Default) => new DecimalLiteral(numberOptions);

        /// <summary>
        /// Builds a parser that matches an quoted string that can be escaped.
        /// </summary>
        public Parser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes);

        /// <summary>
        /// Builds a parser that matches an identifier.
        /// </summary>
        public Parser<TextSpan> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier(extraStart, extraPart);
    }
}
