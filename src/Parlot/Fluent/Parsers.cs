using System;
using System.Collections.Generic;
using System.Numerics;

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
        public static Parser<IReadOnlyList<T>> Separated<U, T>(Parser<U> separator, Parser<T> parser) => new Separated<U, T>(separator, parser);

        /// <summary>
        /// Builds a parser that skips white spaces before another one.
        /// </summary>
        public static Parser<T> SkipWhiteSpace<T>(Parser<T> parser) => new SkipWhiteSpace<T>(parser);

        /// <summary>
        /// Builds a parser that looks for zero or one time the specified parser.
        /// </summary>
        public static Parser<T> ZeroOrOne<T>(Parser<T> parser, T defaultValue = default) => new ZeroOrOne<T>(parser, defaultValue);

        /// <summary>
        /// Builds a parser that looks for zero or many times the specified parser.
        /// </summary>
        public static Parser<IReadOnlyList<T>> ZeroOrMany<T>(Parser<T> parser) => new ZeroOrMany<T>(parser);

        /// <summary>
        /// Builds a parser that looks for one or many times the specified parser.
        /// </summary>
        public static Parser<IReadOnlyList<T>> OneOrMany<T>(Parser<T> parser) => new OneOrMany<T>(parser);

        /// <summary>
        /// Builds a parser that succeeds when the specified parser fails to match.
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
        /// Builds a parser that captures the output of another parser.
        /// This is used to provide pattern matching capabilities, and optimized compiled parsers that then don't need to materialize each parser result.
        /// </summary>
        public static Parser<TextSpan> Capture<T>(Parser<T> parser) => new Capture<T>(parser);

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<T> Always<T>() => new Always<T>();

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<object> Always() => new Always<object>();

        /// <summary>
        /// Builds a parser that always succeeds.
        /// </summary>
        public static Parser<T> Always<T>(T value) => new Always<T>(value);

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
        public Parser<TextSpan> NonWhiteSpace(bool includeNewLines = true) => new NonWhiteSpaceLiteral(includeNewLines: includeNewLines);

        /// <summary>
        /// Builds a parser that matches the specified text.
        /// </summary>
        public Parser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public Parser<char> Char(char c) => new CharLiteral(c);

        /// <summary>
        /// Builds a parser that matches a number and returns any numeric type.
        /// </summary>
        public Parser<T> Number<T>(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = '.', char groupSeparator = ',')
#if NET8_0_OR_GREATER
        where T : INumber<T> 
#endif
        => NumberLiteral.CreateNumberLiteralParser<T>(numberOptions, decimalSeparator, groupSeparator);

        /// <summary>
        /// Builds a parser that matches an integer with an option leading sign.
        /// </summary>
        public Parser<long> Integer(NumberOptions numberOptions = NumberOptions.Integer) => Number<long>(numberOptions);

        /// <summary>
        /// Builds a parser that matches a floating point number represented as a <lang cref="decimal"/> value.
        /// </summary>
        public Parser<decimal> Decimal(NumberOptions numberOptions = NumberOptions.Float) => Number<decimal>(numberOptions);

        /// <summary>
        /// Builds a parser that matches a floating point number represented as a <lang cref="float"/> value.
        /// </summary>
        [Obsolete("Prefer Number<float>(NumberOptions.Float) instead.")]
        public Parser<float> Float(NumberOptions numberOptions = NumberOptions.Float) => Number<float>(numberOptions);

        /// <summary>
        /// Builds a parser that matches a floating point number represented as a <lang cref="double"/> value.
        /// </summary>
        [Obsolete("Prefer Number<double>(NumberOptions.Float) instead.")]
        public Parser<double> Double(NumberOptions numberOptions = NumberOptions.Float) => Number<double>(numberOptions);
        
        /// <summary>
        /// Builds a parser that matches an quoted string that can be escaped.
        /// </summary>
        public Parser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes);

        /// <summary>
        /// Builds a parser that matches an identifier.
        /// </summary>
        public Parser<TextSpan> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier(extraStart, extraPart);

        /// <summary>
        /// Builds a parser that matches a char against a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to match against each char.</param>
        /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
        /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
        public Parser<TextSpan> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0) => new PatternLiteral(predicate, minSize, maxSize);
    }

    public class TermBuilder
    {
        /// <summary>
        /// Builds a parser that matches anything until whitespaces.
        /// </summary>
        public Parser<TextSpan> NonWhiteSpace(bool includeNewLines = true) => Parsers.SkipWhiteSpace(new NonWhiteSpaceLiteral(includeNewLines: includeNewLines));

        /// <summary>
        /// Builds a parser that matches the specified text.
        /// </summary>
        public Parser<string> Text(string text, bool caseInsensitive = false) => Parsers.SkipWhiteSpace(new TextLiteral(text, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

        /// <summary>
        /// Builds a parser that matches the specified char.
        /// </summary>
        public Parser<char> Char(char c) => Parsers.SkipWhiteSpace(new CharLiteral(c));

        /// <summary>
        /// Builds a parser that matches a number and returns any numeric type.
        /// </summary>
        public Parser<T> Number<T>(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = '.', char groupSeparator = ',')
#if NET8_0_OR_GREATER
            where T : INumber<T> 
#endif
            => Parsers.SkipWhiteSpace(NumberLiteral.CreateNumberLiteralParser<T>(numberOptions, decimalSeparator, groupSeparator));

        /// <summary>
        /// Builds a parser that matches an integer with an option leading sign.
        /// </summary>
        public Parser<long> Integer(NumberOptions numberOptions = NumberOptions.Integer) => Parsers.SkipWhiteSpace(Number<long>(numberOptions));

        /// <summary>
        /// Builds a parser that matches a floating point number represented as a <lang cref="decimal"/> value.
        /// </summary>
        public Parser<decimal> Decimal(NumberOptions numberOptions = NumberOptions.Float) => Parsers.SkipWhiteSpace(Number<decimal>(numberOptions));

        /// <summary>
        /// Builds a parser that matches a floating point number represented as a <lang cref="float"/> value.
        /// </summary>
        [Obsolete("Prefer Number<float>(NumberOptions.Float) instead.")] 
        public Parser<float> Float(NumberOptions numberOptions = NumberOptions.Float) => Parsers.SkipWhiteSpace(Number<float>(numberOptions));

        /// <summary>
        /// Builds a parser that matches a floating point number represented as a <lang cref="double"/> value.
        /// </summary>
        [Obsolete("Prefer Number<double>(NumberOptions.Float) instead.")] 
        public Parser<double> Double(NumberOptions numberOptions = NumberOptions.Float) => Parsers.SkipWhiteSpace(Number<double>(numberOptions));

        /// <summary>
        /// Builds a parser that matches an quoted string that can be escaped.
        /// </summary>
        public Parser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => Parsers.SkipWhiteSpace(new StringLiteral(quotes));

        /// <summary>
        /// Builds a parser that matches an identifier.
        /// </summary>
        public Parser<TextSpan> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => Parsers.SkipWhiteSpace(new Identifier(extraStart, extraPart));

        /// <summary>
        /// Builds a parser that matches a char against a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to match against each char.</param>
        /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
        /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
        public Parser<TextSpan> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new PatternLiteral(predicate, minSize, maxSize));
    }
}
