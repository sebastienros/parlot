using Parlot.Rewriting;
using System;
using System.Globalization;
using System.Linq;

namespace Parlot.Fluent;

public abstract partial class Parser<T> : IParser<T>
{
    public abstract bool Parse(ParseContext context, ref ParseResult<T> result);

    /// <summary>
    /// Attempts to parse the input and returns whether the parse was successful.
    /// This is the covariant version of Parse for use with the IParser&lt;out T&gt; interface.
    /// </summary>
    bool IParser<T>.Parse(ParseContext context, out int start, out int end, out object? value)
    {
        var result = new ParseResult<T>();
        var success = Parse(context, ref result);
        start = result.Start;
        end = result.End;
        value = result.Value;
        return success;
    }

    /// <summary>
    /// Builds a parser that converts the previous result when it succeeds.
    /// </summary>
    public Parser<U> Then<U>(Func<T, U> conversion) => new Then<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser that converts the previous result, and can alter the current <see cref="ParseContext"/>.
    /// </summary>
    public Parser<U> Then<U>(Func<ParseContext, T, U> conversion) => new Then<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser that converts the previous result, and can use the <see cref="ParseContext"/> and the start and end offsets.
    /// </summary>
    public Parser<U> Then<U>(Func<ParseContext, int, int, T, U> conversion) => new Then<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser that converts the previous result.
    /// </summary>
    public Parser<U> Then<U>(U value) => new Then<T, U>(this, value);

    /// <summary>
    /// Builds a parser that discards the previous result and returns the default value of type U.
    /// For types that implement IConvertible, attempts type conversion.
    /// </summary>
    public Parser<U?> Then<U>()
    {
        // Check if U implements IConvertible at construction time for performance
        var targetImplementsIConvertible = typeof(IConvertible).IsAssignableFrom(typeof(U));
        
        if (targetImplementsIConvertible)
        {
            return new Then<T, U?>(this, x =>
            {
                // If both T and U are IConvertible, try to convert
                if (x is IConvertible)
                {
                    try
                    {
                        return (U?)Convert.ChangeType(x, typeof(U), CultureInfo.CurrentCulture);
                    }
                    catch
                    {
                        // Fall back to default if conversion fails
                        return default(U);
                    }
                }
                
                // For non-convertible types, return default
                return default(U);
            });
        }
        else
        {
            // For types that don't implement IConvertible, just return default
            return new Then<T, U?>(this, default(U));
        }
    }

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
        Name = name;
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
    /// Builds a parser that ensures the specified parser matches at the current position without consuming input (positive lookahead).
    /// </summary>
    public Parser<T> WhenFollowedBy<U>(Parser<U> lookahead) => new WhenFollowedBy<T>(this, lookahead.Then<object>(_ => new object()));

    /// <summary>
    /// Builds a parser that ensures the specified parser does NOT match at the current position without consuming input (negative lookahead).
    /// </summary>
    public Parser<T> WhenNotFollowedBy<U>(Parser<U> lookahead) => new WhenNotFollowedBy<T>(this, lookahead.Then<object>(_ => new object()));

    /// <summary>
    /// Builds a parser that selects a target parser based on the previous result.
    /// </summary>
    public Parser<U> Switch<U>(Func<ParseContext, T, int> selector, params Parser<U>[] parsers) => new Switch<T, U>(this, selector, parsers);

    /// <summary>
    /// Builds a parser that ensures the cursor is at the end of the input.
    /// </summary>
    public Parser<T> Eof() => new Eof<T>(this);

    /// <summary>
    /// Builds a parser that discards the previous result and replaces it by the specified type or value.
    /// </summary>
    [Obsolete("Use Then<U>() instead.")]
    public Parser<U?> Discard<U>() => new Discard<T, U?>(this, default);

    /// <summary>
    /// Builds a parser that discards the previous result and replaces it by the specified type or value.
    /// </summary>
    [Obsolete("Use Then<U>(value) instead.")]
    public Parser<U> Discard<U>(U value) => new Discard<T, U>(this, value);

    /// <summary>
    /// Builds a parser that returns a default value if the previous parser fails.
    /// </summary>
    public Parser<T> Else(T value) => new Else<T>(this, value);

    /// <summary>
    /// Builds a parser that returns a default value computed by a function if the previous parser fails.
    /// </summary>
    public Parser<T> Else(Func<ParseContext, T> func) => new Else<T>(this, func);

    /// <summary>
    /// Builds a parser that lists all possible matches to improve performance.
    /// </summary>
    public Parser<T> Lookup(bool skipWhiteSpace = false, params ReadOnlySpan<char> expectedChars) => new Seekable<T>(this, skipWhiteSpace, expectedChars);

    /// <summary>
    /// Builds a parser that lists all possible matches to improve performance.
    /// </summary>
    public Parser<T> Lookup(params ISeekable[] parsers) => new Seekable<T>(this, parsers.All(x => x.SkipWhitespace), parsers.SelectMany(x => x.ExpectedChars).ToArray());
}
