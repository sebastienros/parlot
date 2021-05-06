using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public static partial class Parsers<TParseContext>
    where TParseContext : ParseContext
    {
        /// <summary>
        /// Provides parsers for literals. Literals do not skip spaces before being parsed and can be combined to
        /// parse composite terms.
        /// </summary>
        public static LiteralBuilder<TParseContext> Literals => new();

        /// <summary>
        /// Provides parsers for terms. Terms skip spaces before being parsed.
        /// </summary>
        public static TermBuilder<TParseContext> Terms => new();

        /// <summary>
        /// Builds a parser that looks for zero or many times a parser separated by another one.
        /// </summary>
        public static Parser<List<T>, TParseContext> Separated<U, T>(Parser<U, TParseContext> separator, Parser<T, TParseContext> parser) => new Separated<U, T, TParseContext>(separator, parser);

        /// <summary>
        /// Builds a parser that looks for zero or one time the specified parser.
        /// </summary>
        public static Parser<T, TParseContext> ZeroOrOne<T>(Parser<T, TParseContext> parser) => new ZeroOrOne<T, TParseContext>(parser);

        /// <summary>
        /// Builds a parser that creates a scope usable in the specified parser.
        /// </summary>
        public static Parser<T, TParseContext2> Scope<T, TParseContext2>(Parser<T, TParseContext2> parser) where TParseContext2 : ParseContext<TParseContext2> => new ScopedParser<T, TParseContext2>(parser);

        /// <summary>
        /// Builds a parser that creates a scope usable in the specified parser.
        /// </summary>
        public static Parser<T, TParseContext2> Scope<T, TParseContext2>(Action<TParseContext2> action, Parser<T, TParseContext2> parser) where TParseContext2 : ParseContext<TParseContext2> => new ScopedParser<T, TParseContext2>(action, parser);

        /// <summary>
        /// Builds a parser that looks for zero or many times the specified parser.
        /// </summary>
        public static Parser<List<T>, TParseContext> ZeroOrMany<T>(Parser<T, TParseContext> parser) => new ZeroOrMany<T, TParseContext>(parser);

        /// <summary>
        /// Builds a parser that looks for one or many times the specified parser.
        /// </summary>
        public static Parser<List<T>, TParseContext> OneOrMany<T>(Parser<T, TParseContext> parser) => new OneOrMany<T, TParseContext>(parser);

        /// <summary>
        /// Builds a parser that succeed when the specified parser fails to match.
        /// </summary>
        public static Parser<T, TParseContext> Not<T>(Parser<T, TParseContext> parser) => new Not<T, TParseContext>(parser);

        /// <summary>
        /// Builds a parser that can be defined later one. Use it when a parser need to be declared before its rule can be set.
        /// </summary>
        public static Deferred<T, TParseContext> Deferred<T>() => new();

        /// <summary>
        /// Builds a parser than needs a reference to itself to be declared.
        /// </summary>
        public static Deferred<T, TParseContext> Recursive<T>(Func<Deferred<T, TParseContext>, Parser<T, TParseContext>> parser) => new(parser);

        /// <summary>
        /// Builds a parser that matches the specified parser between two other ones.
        /// </summary>
        public static Parser<T, TParseContext> Between<A, T, B>(Parser<A, TParseContext> before, Parser<T, TParseContext> parser, Parser<B, TParseContext> after) => new Between<A, T, B, TParseContext>(before, parser, after);

        /// <summary>
        /// Builds a parser that matches any chars before a specific parser.
        /// </summary>
        public static Parser<TextSpan, TParseContext> AnyCharBefore<T>(Parser<T, TParseContext> parser, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false) => new TextBefore<T, TParseContext>(parser, canBeEmpty, failOnEof, consumeDelimiter);

        /// <summary>
        /// Builds a parser that captures the output of another parser.
        /// </summary>
        public static Parser<TextSpan, TParseContext> Capture<T>(Parser<T, TParseContext> parser) => new Capture<T, TParseContext>(parser);

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<T, TParseContext> Empty<T>() => new Empty<T, TParseContext>();

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<object, TParseContext> Empty() => new Empty<object, TParseContext>();

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<T, TParseContext> Empty<T>(T value) => new Empty<T, TParseContext>(value);

    }

    public static partial class Parsers
    {
        /// <summary>
        /// Builds a parser that creates a scope usable in the specified parser.
        /// </summary>
        public static Parser<T, TParseContext> Scope<T, TParseContext>(Parser<T, TParseContext> parser) where TParseContext : ParseContext<TParseContext> => new ScopedParser<T, TParseContext>(parser);
    }

    public class LiteralBuilder<TParseContext>
    where TParseContext : ParseContext
    {
        /// <summary>
        /// Builds a parser that matches whitespaces.
        /// </summary>
        public Parser<TextSpan, TParseContext> WhiteSpace(bool includeNewLines = false) => new WhiteSpaceLiteral<TParseContext>(includeNewLines);

        /// <summary>
        /// Builds a parser that matches anything until whitespaces.
        /// </summary>
        public Parser<TextSpan, TParseContext> NonWhiteSpace(bool includeNewLines = false) => new NonWhiteSpaceLiteral<TParseContext>(skipWhiteSpace: false, includeNewLines: includeNewLines);

        /// <summary>
        /// Builds a parser that matches the specified text.
        /// </summary>
        public Parser<string, TParseContext> Text(string text, bool caseInsensitive = false) => new TextLiteral<TParseContext>(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null, skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public Parser<char, TParseContext> Char(char c) => new CharLiteral<TParseContext>(c, skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches an integer.
        /// </summary>
        public Parser<long, TParseContext> Integer() => new IntegerLiteral<TParseContext>(skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches a floating point number.
        /// </summary>
        public Parser<decimal, TParseContext> Decimal() => new DecimalLiteral<TParseContext>(skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches an quoted string that can be escaped.
        /// </summary>
        public Parser<TextSpan, TParseContext> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral<TParseContext>(quotes, skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches an identifier.
        /// </summary>
        public Parser<TextSpan, TParseContext> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier<TParseContext>(extraStart, extraPart, skipWhiteSpace: false);

        /// <summary>
        /// Builds a parser that matches a char against a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to match against each char.</param>
        /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
        /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
        public Parser<TextSpan, TParseContext> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0) => new PatternLiteral<TParseContext>(predicate, minSize, maxSize, skipWhiteSpace: false);
    }

    public class TermBuilder<TParseContext>
    where TParseContext : ParseContext
    {
        /// <summary>
        /// Builds a parser that matches anything until whitespaces.
        /// </summary>
        public Parser<TextSpan, TParseContext> NonWhiteSpace(bool includeNewLines = false) => new NonWhiteSpaceLiteral<TParseContext>(includeNewLines: includeNewLines);

        /// <summary>
        /// Builds a parser that matches the specified text.
        /// </summary>
        public Parser<string, TParseContext> Text(string text, bool caseInsensitive = false) => new TextLiteral<TParseContext>(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null);

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public Parser<char, TParseContext> Char(char c) => new CharLiteral<TParseContext>(c);

        /// <summary>
        /// Builds a parser that matches an integer.
        /// </summary>
        public Parser<long, TParseContext> Integer(NumberOptions numberOptions = NumberOptions.Default) => new IntegerLiteral<TParseContext>(numberOptions);

        /// <summary>
        /// Builds a parser that matches a floating point number.
        /// </summary>
        public Parser<decimal, TParseContext> Decimal(NumberOptions numberOptions = NumberOptions.Default) => new DecimalLiteral<TParseContext>(numberOptions);

        /// <summary>
        /// Builds a parser that matches an quoted string that can be escaped.
        /// </summary>
        public Parser<TextSpan, TParseContext> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral<TParseContext>(quotes);

        /// <summary>
        /// Builds a parser that matches an identifier.
        /// </summary>
        public Parser<TextSpan, TParseContext> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier<TParseContext>(extraStart, extraPart);

        /// <summary>
        /// Builds a parser that matches a char against a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to match against each char.</param>
        /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
        /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
        public Parser<TextSpan, TParseContext> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0) => new PatternLiteral<TParseContext>(predicate, minSize, maxSize);
    }
}
