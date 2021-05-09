
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent
{
    public static class StringParsers<TParseContext>
    where TParseContext : ParseContextWithScanner<char>
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
        /// Builds a parser that return either of the first successful of the specified parsers.
        /// </summary>
        public static Parser<T, TParseContext, char> OneOf<T>(params Parser<T, TParseContext>[] parsers) => Parsers<TParseContext, char>.OneOf(parsers);

        /// <summary>
        /// Builds a parser that looks for zero or many times a parser separated by another one.
        /// </summary>
        public static Parser<List<T>, TParseContext, char> Separated<U, T>(Parser<U, TParseContext> separator, Parser<T, TParseContext> parser) => Parsers<TParseContext, char>.Separated(separator, parser);

        /// <summary>
        /// Builds a parser that looks for zero or one time the specified parser.
        /// </summary>
        public static Parser<T, TParseContext, char> ZeroOrOne<T>(Parser<T, TParseContext> parser) => Parsers<TParseContext, char>.ZeroOrOne(parser);

        /// <summary>
        /// Builds a parser that creates a scope usable in the specified parser.
        /// </summary>
        public static Parser<T, TParseContext2, char> Scope<T, TParseContext2>(Parser<T, TParseContext2, char> parser) where TParseContext2 : ParseContext<char, TParseContext2> => Parsers<TParseContext, char>.Scope(parser);

        /// <summary>
        /// Builds a parser that looks for zero or many times the specified parser.
        /// </summary>
        public static Parser<List<T>, TParseContext, char> ZeroOrMany<T>(Parser<T, TParseContext> parser) => Parsers<TParseContext, char>.ZeroOrMany(parser);

        /// <summary>
        /// Builds a parser that looks for one or many times the specified parser.
        /// </summary>
        public static Parser<List<T>, TParseContext, char> OneOrMany<T>(Parser<T, TParseContext> parser) => Parsers<TParseContext, char>.OneOrMany(parser);

        /// <summary>
        /// Builds a parser that succeed when the specified parser fails to match.
        /// </summary>
        public static Parser<T, TParseContext> Not<T>(Parser<T, TParseContext> parser) => Parsers<TParseContext, char>.Not(parser);

        /// <summary>
        /// Builds a parser that succeed when the specified parser fails to match.
        /// </summary>
        public static Parser<T, TParseContext, char> Not<T>(Parser<T, TParseContext, char> parser) => Parsers<TParseContext, char>.Not(parser);

        /// <summary>
        /// Builds a parser that can be defined later one. Use it when a parser need to be declared before its rule can be set.
        /// </summary>
        public static Deferred<T, TParseContext, char> Deferred<T>() => Parsers<TParseContext, char>.Deferred<T>();

        /// <summary>
        /// Builds a parser than needs a reference to itself to be declared.
        /// </summary>
        public static Deferred<T, TParseContext> Recursive<T>(Func<Deferred<T, TParseContext>, Parser<T, TParseContext>> parser) => Parsers<TParseContext, char>.Recursive(parser);

        /// <summary>
        /// Builds a parser than needs a reference to itself to be declared.
        /// </summary>
        public static Deferred<T, TParseContext, char> Recursive<T>(Func<Deferred<T, TParseContext, char>, Parser<T, TParseContext, char>> parser) => Parsers<TParseContext, char>.Recursive(parser);

        /// <summary>
        /// Builds a parser that matches the specified parser between two other ones.
        /// </summary>
        public static Parser<T, TParseContext, char> Between<A, T, B>(Parser<A, TParseContext> before, Parser<T, TParseContext> parser, Parser<B, TParseContext> after) => Parsers<TParseContext, char>.Between(before, parser, after);

        /// <summary>
        /// Builds a parser that matches any chars before a specific parser.
        /// </summary>
        public static Parser<BufferSpan<char>, TParseContext, char> AnyCharBefore<T>(Parser<T, TParseContext> parser, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false) => Parsers<TParseContext, char>.AnyCharBefore(parser, canBeEmpty, failOnEof, consumeDelimiter);

        /// <summary>
        /// Builds a parser that captures the output of another parser.
        /// </summary>
        public static Parser<BufferSpan<char>, TParseContext, char> Capture<T>(Parser<T, TParseContext> parser) => Parsers<TParseContext, char>.Capture<T>(parser);

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<T, TParseContext, char> Empty<T>() => Parsers<TParseContext, char>.Empty<T>();

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<object, TParseContext, char> Empty() => Parsers<TParseContext, char>.Empty();

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<T, TParseContext, char> Empty<T>(T value) => Parsers<TParseContext, char>.Empty(value);

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public static Parser<char, TParseContext, char> Char(char c) => Parsers<TParseContext, char>.Char(c);

        /// <summary>
        /// Builds a parser that matches a char against a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to match against each char.</param>
        /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
        /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
        public static Parser<BufferSpan<char>, TParseContext, char> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0) => StringParsers<TParseContext>.SkipWhiteSpace(Parsers<TParseContext, char>.Pattern(predicate, minSize, maxSize));

        /// <summary>
        /// Builds a parser that skips white spaces before another one.
        /// </summary>
        public static Parser<T, TParseContext, char> SkipWhiteSpace<T>(Parser<T, TParseContext> parser) => new SkipWhiteSpace<T, TParseContext>(parser);
    }


    public class LiteralBuilder<TParseContext>
    where TParseContext : ParseContextWithScanner<char>
    {
        /// <summary>
        /// Builds a parser that matches whitespaces.
        /// </summary>
        public Parser<BufferSpan<char>, TParseContext, char> WhiteSpace(bool includeNewLines = false) => new WhiteSpaceLiteral<TParseContext>(includeNewLines);

        /// <summary>
        /// Builds a parser that matches anything until whitespaces.
        /// </summary>
        public Parser<BufferSpan<char>, TParseContext, char> NonWhiteSpace(bool includeNewLines = false) => new NonWhiteSpaceLiteral<TParseContext>(includeNewLines: includeNewLines);

        /// <summary>
        /// Builds a parser that matches the specified text.
        /// </summary>
        public Parser<string, TParseContext, char> Text(string text, bool caseInsensitive = false) => new TextLiteral<TParseContext>(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null);

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public Parser<char, TParseContext, char> Char(char c) => Parsers<TParseContext, char>.Char(c);

        /// <summary>
        /// Builds a parser that matches an integer.
        /// </summary>
        public Parser<long, TParseContext, char> Integer() => new IntegerLiteral<TParseContext>();

        /// <summary>
        /// Builds a parser that matches a floating point number.
        /// </summary>
        public Parser<decimal, TParseContext, char> Decimal() => new DecimalLiteral<TParseContext>();

        /// <summary>
        /// Builds a parser that matches an quoted string that can be escaped.
        /// </summary>
        public Parser<BufferSpan<char>, TParseContext, char> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral<TParseContext>(quotes);

        /// <summary>
        /// Builds a parser that matches an identifier.
        /// </summary>
        public Parser<BufferSpan<char>, TParseContext, char> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier<TParseContext>(extraStart, extraPart);

        /// <summary>
        /// Builds a parser that matches a char against a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to match against each char.</param>
        /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
        /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Parser<BufferSpan<char>, TParseContext, char> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0) => Parsers<TParseContext, char>.Pattern(predicate, minSize, maxSize);
    }

    public class TermBuilder<TParseContext>
    where TParseContext : ParseContextWithScanner<char>
    {
        /// <summary>
        /// Builds a parser that matches anything until whitespaces.
        /// </summary>
        public Parser<BufferSpan<char>, TParseContext, char> NonWhiteSpace(bool includeNewLines = false) => StringParsers<TParseContext>.SkipWhiteSpace(new NonWhiteSpaceLiteral<TParseContext>(includeNewLines: includeNewLines));

        /// <summary>
        /// Builds a parser that matches the specified text.
        /// </summary>
        public Parser<string, TParseContext, char> Text(string text, bool caseInsensitive = false) => StringParsers<TParseContext>.SkipWhiteSpace(new TextLiteral<TParseContext>(text, comparer: caseInsensitive ? StringComparer.OrdinalIgnoreCase : null));

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public Parser<char, TParseContext, char> Char(char c) => StringParsers<TParseContext>.SkipWhiteSpace(new CharLiteral<char, TParseContext>(c));

        /// <summary>
        /// Builds a parser that matches an integer.
        /// </summary>
        public Parser<long, TParseContext, char> Integer(NumberOptions numberOptions = NumberOptions.Default) => StringParsers<TParseContext>.SkipWhiteSpace(new IntegerLiteral<TParseContext>(numberOptions));

        /// <summary>
        /// Builds a parser that matches a floating point number.
        /// </summary>
        public Parser<decimal, TParseContext, char> Decimal(NumberOptions numberOptions = NumberOptions.Default) => StringParsers<TParseContext>.SkipWhiteSpace(new DecimalLiteral<TParseContext>(numberOptions));

        /// <summary>
        /// Builds a parser that matches an quoted string that can be escaped.
        /// </summary>
        public Parser<BufferSpan<char>, TParseContext, char> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => StringParsers<TParseContext>.SkipWhiteSpace(new StringLiteral<TParseContext>(quotes));

        /// <summary>
        /// Builds a parser that matches an identifier.
        /// </summary>
        public Parser<BufferSpan<char>, TParseContext, char> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => StringParsers<TParseContext>.SkipWhiteSpace(new Identifier<TParseContext>(extraStart, extraPart));

        /// <summary>
        /// Builds a parser that matches a char against a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to match against each char.</param>
        /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
        /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Parser<BufferSpan<char>, TParseContext, char> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0) => StringParsers<TParseContext>.SkipWhiteSpace(Parsers<TParseContext, char>.Pattern(predicate, minSize, maxSize));
    }
}