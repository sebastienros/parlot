using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Buffers;
using System.Numerics;
#endif

#pragma warning disable CA1822 // Mark members as static

namespace Parlot.Fluent;

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
    public static Parser<T> ZeroOrOne<T>(Parser<T> parser, T defaultValue) => new ZeroOrOne<T>(parser, defaultValue);

    /// <summary>
    /// Builds a parser that looks for zero or one time the specified parser.
    /// </summary>
    public static Parser<T> ZeroOrOne<T>(Parser<T> parser) where T : notnull => new ZeroOrOne<T>(parser, default!);

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
    /// Builds a parser that invoked the next one if a condition is true.
    /// </summary>
    [Obsolete("Use the Select parser instead.")]
    public static Parser<T> If<C, S, T>(Func<C, S?, bool> predicate, S? state, Parser<T> parser) where C : ParseContext => new If<C, S, T>(parser, predicate, state);

    /// <summary>
    /// Builds a parser that invoked the next one if a condition is true.
    /// </summary>
    [Obsolete("Use the Select parser instead.")]
    public static Parser<T> If<S, T>(Func<ParseContext, S?, bool> predicate, S? state, Parser<T> parser) => new If<ParseContext, S, T>(parser, predicate, state);

    /// <summary>
    /// Builds a parser that invoked the next one if a condition is true.
    /// </summary>
    [Obsolete("Use the Select parser instead.")]
    public static Parser<T> If<C, T>(Func<C, bool> predicate, Parser<T> parser) where C : ParseContext => new If<C, object?, T>(parser, (c, s) => predicate(c), null);

    /// <summary>
    /// Builds a parser that invoked the next one if a condition is true.
    /// </summary>
    [Obsolete("Use the Select parser instead.")]
    public static Parser<T> If<T>(Func<ParseContext, bool> predicate, Parser<T> parser) => new If<ParseContext, object?, T>(parser, (c, s) => predicate(c), null);

    /// <summary>
    /// Builds a parser that selects another parser using custom logic.
    /// </summary>
    public static Parser<T> Select<C, T>(Func<C, Parser<T>> selector) where C : ParseContext => new Select<C, T>(selector);

    /// <summary>
    /// Builds a parser that selects another parser using custom logic.
    /// </summary>
    public static Parser<T> Select<T>(Func<ParseContext, Parser<T>> selector) => new Select<ParseContext, T>(selector);

    /// <summary>
    /// Builds a parser that can be defined later on. Use it when a parser need to be declared before its rule can be set.
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
    public static Parser<object?> Always() => Always<object>();

    /// <summary>
    /// Builds a parser that always succeeds.
    /// </summary>
    public static Parser<T?> Always<T>() => new Always<T?>(default);

    /// <summary>
    /// Builds a parser that always succeeds.
    /// </summary>
    public static Parser<T> Always<T>(T value) => new Always<T>(value);

    /// <summary>
    /// Builds a parser that always fails.
    /// </summary>
    public static Parser<T> Fail<T>() => new Fail<T>();

    /// <summary>
    /// Builds a parser that always fails.
    /// </summary>
    public static Parser<object> Fail() => new Fail<object>();
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
    => NumberLiterals.CreateNumberLiteralParser<T>(numberOptions, decimalSeparator, groupSeparator);

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
    /// Builds a parser that matches an identifier which can have a different starting value that the rest of its chars.
    /// </summary>
    public Parser<TextSpan> Identifier(Func<char, bool>? extraStart = null, Func<char, bool>? extraPart = null)
    {
#if NET8_0_OR_GREATER
        if (extraStart == null && extraPart == null)
        {
            return new IdentifierLiteral(Character._identifierStart, Character._identifierPart);
        }
        else
        {
            // IdentifierLiteral doesn't support the Func<,> overload
            return new Identifier(extraStart, extraPart);
        }
#else
        return new Identifier(extraStart, extraPart);
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Builds a parser that matches an identifier which can have a different starting value that the rest of its chars.
    /// </summary>
    public Parser<TextSpan> Identifier(SearchValues<char> identifierStart, SearchValues<char> identifierPart) => new IdentifierLiteral(identifierStart, identifierPart);

    /// <summary>
    /// Builds a parser that matches an identifier which can have a different starting value that the rest of its chars.
    /// </summary>
    public Parser<TextSpan> Identifier(ReadOnlySpan<char> identifierStart, ReadOnlySpan<char> identifierPart) => new IdentifierLiteral(SearchValues.Create(identifierStart), SearchValues.Create(identifierPart));

#endif

    /// <summary>
    /// Builds a parser that matches a char against a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match against each char.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0) => new PatternLiteral(predicate, minSize, maxSize);

#if NET8_0_OR_GREATER
    /// <summary>
    /// Builds a parser that matches a list of chars.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance to match against each char.</param>
    /// <param name="minSize">The minimum number of chars required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> AnyOf(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0) => new SearchValuesCharLiteral(searchValues, minSize, maxSize);

    /// <summary>
    /// Builds a parser that matches a list of chars.
    /// </summary>
    /// <param name="values">The set of chars to match.</param>
    /// <param name="minSize">The minimum number of chars required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> AnyOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => new SearchValuesCharLiteral(values, minSize, maxSize);

    /// <summary>
    /// Builds a parser that matches anything but a list of chars.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance to ignore against each char.</param>
    /// <param name="minSize">The minimum number of chars required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of chars it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> NoneOf(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0) => new SearchValuesCharLiteral(searchValues, minSize, maxSize, negate: true);

    /// <summary>
    /// Builds a parser that matches anything but a list of chars.
    /// </summary>
    /// <param name="values">The set of chars not to match.</param>
    /// <param name="minSize">The minimum number of chars required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of chars it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> NoneOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => new SearchValuesCharLiteral(values, minSize, maxSize, negate: true);
#else
    /// <summary>
    /// Builds a parser that matches a list of chars.
    /// </summary>
    /// <param name="values">The set of chars to match.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> AnyOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => new ListOfChars(values, minSize, maxSize);

    /// <summary>
    /// Builds a parser that matches anything but a list of chars.
    /// </summary>
    /// <param name="values">The set of chars not to match.</param>
    /// <param name="minSize">The minimum number of required chars. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of chars it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> NoneOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => new ListOfChars(values, minSize, maxSize, negate: true);
#endif
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
        => Parsers.SkipWhiteSpace(NumberLiterals.CreateNumberLiteralParser<T>(numberOptions, decimalSeparator, groupSeparator));

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
    public Parser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => Parsers.SkipWhiteSpace(new StringLiteral(quotes));

    /// <summary>
    /// Builds a parser that matches an identifier which can have a different starting value that the rest of its chars.
    /// </summary>
    public Parser<TextSpan> Identifier(Func<char, bool>? extraStart = null, Func<char, bool>? extraPart = null)
    {
#if NET8_0_OR_GREATER
        if (extraStart == null && extraPart == null)
        {
            return Parsers.SkipWhiteSpace(new IdentifierLiteral(Character._identifierStart, Character._identifierPart));
        }
        else
        {
            // IdentifierLiteral doesn't support the Func<,> overload
            return Parsers.SkipWhiteSpace(new Identifier(extraStart, extraPart));
        }
#else
        return Parsers.SkipWhiteSpace(new Identifier(extraStart, extraPart));
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Builds a parser that matches an identifier which can have a different starting value that the rest of its chars.
    /// </summary>
    public Parser<TextSpan> Identifier(SearchValues<char> identifierStart, SearchValues<char> identifierPart) => Parsers.SkipWhiteSpace(new IdentifierLiteral(identifierStart, identifierPart));

    /// <summary>
    /// Builds a parser that matches an identifier which can have a different starting value that the rest of its chars.
    /// </summary>
    public Parser<TextSpan> Identifier(ReadOnlySpan<char> identifierStart, ReadOnlySpan<char> identifierPart) => Parsers.SkipWhiteSpace(new IdentifierLiteral(SearchValues.Create(identifierStart), SearchValues.Create(identifierPart)));

#endif

    /// <summary>
    /// Builds a parser that matches a char against a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match against each char.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new PatternLiteral(predicate, minSize, maxSize));

#if NET8_0_OR_GREATER
    /// <summary>
    /// Builds a parser that matches a list of chars.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance to match against each char.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> AnyOf(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new SearchValuesCharLiteral(searchValues, minSize, maxSize));

    /// <summary>
    /// Builds a parser that matches a list of chars.
    /// </summary>
    /// <param name="values">The set of char to match.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> AnyOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(AnyOf(SearchValues.Create(values), minSize, maxSize));

    /// <summary>
    /// Builds a parser that matches anything but a list of chars.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance to ignore against each char.</param>
    /// <param name="minSize">The minimum number of chars required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of chars it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> NoneOf(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new SearchValuesCharLiteral(searchValues, minSize, maxSize, negate: true));

    /// <summary>
    /// Builds a parser that matches anything but a list of chars.
    /// </summary>
    /// <param name="values">The set of chars not to match.</param>
    /// <param name="minSize">The minimum number of chars required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of chars it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> NoneOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new SearchValuesCharLiteral(values, minSize, maxSize, negate: true));
#else
    /// <summary>
    /// Builds a parser that matches a list of chars.
    /// </summary>
    /// <param name="values">The set of char to match.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> AnyOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new ListOfChars(values, minSize, maxSize));

    /// <summary>
    /// Builds a parser that matches anything but a list of chars.
    /// </summary>
    /// <param name="values">The set of chars not to match.</param>
    /// <param name="minSize">The minimum number of required chars. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of chars it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<TextSpan> NoneOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => new ListOfChars(values, minSize, maxSize, negate: true);
#endif
}
