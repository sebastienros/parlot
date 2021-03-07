using System;

namespace Parlot.Fluent
{
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
        /// Builds a parser that converts the previous result when it fails.
        /// </summary>
        public Parser<U> Else<U>(Func<T, U> conversion) => new Else<T, U>(this, conversion);

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
        /// Builds a parser that verifies the previous parser result matches a predicate.
        /// </summary>
        public Parser<T> When(Func<T, bool> predicate) => new When<T>(this, predicate);

        /// <summary>
        /// Builds a parser what returns another one based on the previous result.
        /// </summary>
        public Parser<U> Switch<U>(Func<ParseContext, T, Parser<U>> action) => new Switch<T, U>(this, action);

        /// <summary>
        /// Builds a parser that ensure the cursor is tat the end of the input.
        /// </summary>
        public Parser<T> Eof() => new Eof<T>(this);

        /// <summary>
        /// Builds a parser that discards the previous result and replaces it by the specified type or value.
        /// </summary>
        public Parser<U> Discard<U>() => new Discard<T, U>(this);

        /// <summary>
        /// Builds a parser that discards the previous result and replaces it by the specified type or value.
        /// </summary>
        public Parser<U> Discard<U>(U value) => new Discard<T, U>(this, value);
    }
}
