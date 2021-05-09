using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent
{
    public static class Parsers<TParseContext, TChar>
    where TParseContext : ParseContextWithScanner<TChar>
    where TChar : IEquatable<TChar>, IConvertible
    {
        /// <summary>
        /// Builds a parser that return either of the first successful of the specified parsers.
        /// </summary>
        public static Parser<T, TParseContext, TChar> OneOf<T>(params Parser<T, TParseContext>[] parsers) => new OneOf<T, TParseContext, TChar>(parsers);

        /// <summary>
        /// Builds a parser that looks for zero or many times a parser separated by another one.
        /// </summary>
        public static Parser<List<T>, TParseContext, TChar> Separated<U, T>(Parser<U, TParseContext> separator, Parser<T, TParseContext> parser) => new Separated<U, T, TParseContext, TChar>(separator, parser);

        /// <summary>
        /// Builds a parser that looks for zero or one time the specified parser.
        /// </summary>
        public static Parser<T, TParseContext, TChar> ZeroOrOne<T>(Parser<T, TParseContext> parser) => new ZeroOrOne<T, TParseContext, TChar>(parser);

        /// <summary>
        /// Builds a parser that creates a scope usable in the specified parser.
        /// </summary>
        public static Parser<T, TParseContext2, TChar> Scope<T, TParseContext2>(Parser<T, TParseContext2> parser) where TParseContext2 : ParseContext<TChar, TParseContext2> => new ScopedParser<T, TParseContext2, TChar>(parser);

        /// <summary>
        /// Builds a parser that creates a scope usable in the specified parser.
        /// </summary>
        public static Parser<T, TParseContext2> Scope<T, TParseContext2>(Action<TParseContext2> action, Parser<T, TParseContext2> parser) where TParseContext2 : ParseContext<TChar, TParseContext2> => new ScopedParser<T, TParseContext2, TChar>(action, parser);

        /// <summary>
        /// Builds a parser that looks for zero or many times the specified parser.
        /// </summary>
        public static Parser<List<T>, TParseContext, TChar> ZeroOrMany<T>(Parser<T, TParseContext> parser) => new ZeroOrMany<T, TParseContext, TChar>(parser);

        /// <summary>
        /// Builds a parser that looks for one or many times the specified parser.
        /// </summary>
        public static Parser<List<T>, TParseContext, TChar> OneOrMany<T>(Parser<T, TParseContext> parser) => new OneOrMany<T, TParseContext, TChar>(parser);

        /// <summary>
        /// Builds a parser that succeed when the specified parser fails to match.
        /// </summary>
        public static Parser<T, TParseContext, TChar> Not<T>(Parser<T, TParseContext> parser) => new Not<T, TParseContext, TChar>(parser);

        /// <summary>
        /// Builds a parser that can be defined later one. Use it when a parser need to be declared before its rule can be set.
        /// </summary>
        public static Deferred<T, TParseContext, TChar> Deferred<T>() => new();

        /// <summary>
        /// Builds a parser than needs a reference to itself to be declared.
        /// </summary>
        public static Deferred<T, TParseContext> Recursive<T>(Func<Deferred<T, TParseContext>, Parser<T, TParseContext>> parser) => new(parser);

        /// <summary>
        /// Builds a parser than needs a reference to itself to be declared.
        /// </summary>
        public static Deferred<T, TParseContext, TChar> Recursive<T>(Func<Deferred<T, TParseContext, TChar>, Parser<T, TParseContext, TChar>> parser) => new(parser);

        /// <summary>
        /// Builds a parser that matches the specified parser between two other ones.
        /// </summary>
        public static Parser<T, TParseContext, TChar> Between<A, T, B>(Parser<A, TParseContext> before, Parser<T, TParseContext> parser, Parser<B, TParseContext> after) => new Between<A, T, B, TParseContext, TChar>(before, parser, after);

        /// <summary>
        /// Builds a parser that matches any chars before a specific parser.
        /// </summary>
        public static Parser<BufferSpan<TChar>, TParseContext, TChar> AnyCharBefore<T>(Parser<T, TParseContext> parser, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false) => new TextBefore<T, TParseContext, TChar>(parser, canBeEmpty, failOnEof, consumeDelimiter);

        /// <summary>
        /// Builds a parser that captures the output of another parser.
        /// </summary>
        public static Parser<BufferSpan<TChar>, TParseContext, TChar> Capture<T>(Parser<T, TParseContext> parser) => new Capture<T, TParseContext, TChar>(parser);

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<T, TParseContext, TChar> Empty<T>() => new Empty<T, TParseContext, TChar>();

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<object, TParseContext, TChar> Empty() => new Empty<object, TParseContext, TChar>();

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<T, TParseContext, TChar> Empty<T>(T value) => new Empty<T, TParseContext, TChar>(value);

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public static Parser<TChar, TParseContext, TChar> Char(TChar c) => new CharLiteral<TChar, TParseContext>(c);

        /// <summary>
        /// Builds a parser that matches a char against a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to match against each char.</param>
        /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
        /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
        public static Parser<BufferSpan<TChar>, TParseContext, TChar> Pattern(Func<TChar, bool> predicate, int minSize = 1, int maxSize = 0) => new PatternLiteral<TParseContext, TChar>(predicate, minSize, maxSize);
    }

    public partial class Parsers
    {
        /// <summary>
        /// Builds a parser that creates a scope usable in the specified parser.
        /// </summary>
        public static Parser<T, TParseContext, TChar> Scope<T, TParseContext, TChar>(Parser<T, TParseContext, TChar> parser) where TParseContext : ParseContext<TChar, TParseContext> where TChar : IEquatable<TChar>, IConvertible => new ScopedParser<T, TParseContext, TChar>(parser);

    }
}
