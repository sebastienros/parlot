using Parlot.Rewriting;
using System;
using System.Linq;

namespace Parlot.Fluent;

public abstract partial class Parser<T>
{
    public abstract bool Parse(ParseContext context, ref ParseResult<T> result);

    /// <summary>
    /// Builds a parser that converts the previous result when it succeeds.
    /// </summary>
    public Parser<U> Then<U>(Func<T, U> conversion) => new Then<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser that converts the previous result, and can alter the current <see cref="ParseContext"/>.
    /// </summary>
    public Parser<U> Then<U>(Func<ParseContext, T, U> conversion) => new Then<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser that converts the previous result.
    /// </summary>
    public Parser<U> Then<U>(U value) => new Then<T, U>(this, value);

    /// <summary>
    /// Builds a parser that converts the previous result when it succeeds or returns a default value if it fails.
    /// </summary>
    public Parser<U> ThenElse<U>(Func<T, U> conversion, U elseValue) => new Then<T, U>(this, conversion).Else(elseValue);

    /// <summary>
    /// Builds a parser that converts the previous result or returns a default value if it fails, and can alter the current <see cref="ParseContext"/>.
    /// </summary>
    public Parser<U> ThenElse<U>(Func<ParseContext, T, U> conversion, U elseValue) => new Then<T, U>(this, conversion).Else(elseValue);

    /// <summary>
    /// Builds a parser that converts the previous result or returns a default value if it fails.
    /// </summary>
    public Parser<U> ThenElse<U>(U value, U elseValue) => new Then<T, U>(this, value).Else(elseValue);

    /// <summary>
    /// Builds a parser that emits an error when the previous parser failed.
    /// </summary>
    public Parser<T> ElseError(string message) => new ElseError<T>(this, message);

    /// <summary>
    /// Builds a parser that emits an error.
    /// </summary>
    public Parser<T> Error(string message) => new Error<T>(this, message);

    /// <summary>
    /// Builds a parser that emits an error.
    /// </summary>
    public Parser<U> Error<U>(string message) => new Error<T, U>(this, message);

    /// <summary>
    /// Names a parser.
    /// </summary>
    public Parser<T> Named(string name)
    {
        this.Name = name;
        return this;
    }

    /// <summary>
    /// Builds a parser that verifies the previous parser result matches a predicate.
    /// </summary>
    [Obsolete("Use When(Func<ParseContext, T, bool> predicate) instead.")]
    public Parser<T> When(Func<T, bool> predicate) => new When<T>(this, predicate);

    /// <summary>
    /// Builds a parser that verifies the previous parser result matches a predicate.
    /// </summary>
    public Parser<T> When(Func<ParseContext, T, bool> predicate) => new When<T>(this, predicate);

    /// <summary>
    /// Builds a parser what returns another one based on the previous result.
    /// </summary>
    public Parser<U> Switch<U>(Func<ParseContext, T, Parser<U>> action) => new Switch<T, U>(this, action);

    /// <summary>
    /// Builds a parser that ensures the cursor is at the end of the input.
    /// </summary>
    public Parser<T> Eof() => new Eof<T>(this);

    /// <summary>
    /// Builds a parser that discards the previous result and replaces it by the specified type or value.
    /// </summary>
    public Parser<U?> Discard<U>() => new Discard<T, U?>(this, default);

    /// <summary>
    /// Builds a parser that discards the previous result and replaces it by the specified type or value.
    /// </summary>
    public Parser<U> Discard<U>(U value) => new Discard<T, U>(this, value);

    /// <summary>
    /// Builds a parser that returns a default value if the previous parser fails.
    /// </summary>
    public Parser<T> Else(T value) => new Else<T>(this, value);

    /// <summary>
    /// Builds a parser that lists all possible matches to improve performance.
    /// </summary>
    public Parser<T> Lookup(bool skipWhiteSpace = false, params ReadOnlySpan<char> expectedChars) => new Seekable<T>(this, skipWhiteSpace, expectedChars);

    /// <summary>
    /// Builds a parser that lists all possible matches to improve performance.
    /// </summary>
    public Parser<T> Lookup(params ISeekable[] parsers) => new Seekable<T>(this, parsers.All(x => x.SkipWhitespace), parsers.SelectMany(x => x.ExpectedChars).ToArray());
}
