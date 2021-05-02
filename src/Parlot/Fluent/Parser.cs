using System;

namespace Parlot.Fluent
{
    public abstract partial class Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        public abstract bool Parse(TParseContext context, ref ParseResult<T> result);

        /// <summary>
        /// Builds a parser that converts the previous result when it succeeds.
        /// </summary>
        public Parser<U, TParseContext> Then<U>(Func<T, U> conversion) => new Then<T, U, TParseContext>(this, conversion);

        /// <summary>
        /// Builds a parser that converts the previous result, and can alter the current <see cref="ParseContext"/>.
        /// </summary>
        public Parser<U, TParseContext> Then<U>(Func<TParseContext, T, U> conversion) => new Then<T, U, TParseContext>(this, conversion);

        /// <summary>
        /// Builds a parser that converts the previous result when it succeeds.
        /// </summary>
        public Parser<T, TParseContext> Then(Action<T> action) => new Then<T, TParseContext>(this, action);

        /// <summary>
        /// Builds a parser that converts the previous result, and can alter the current <see cref="ParseContext"/>.
        /// </summary>
        public Parser<T, TParseContext> Then(Action<TParseContext, T> action) => new Then<T, TParseContext>(this, action);

        /// <summary>
        /// Builds a parser that emits an error when the previous parser failed.
        /// </summary>
        public Parser<T, TParseContext> ElseError(string message) => new ElseError<T, TParseContext>(this, message);

        /// <summary>
        /// Builds a parser that emits an error.
        /// </summary>
        public Parser<T, TParseContext> Error(string message) => new Error<T, TParseContext>(this, message);

        /// <summary>
        /// Builds a parser that emits an error.
        /// </summary>
        public Parser<U, TParseContext> Error<U>(string message) => new Error<T, U, TParseContext>(this, message);

        /// <summary>
        /// Builds a parser that verifies the previous parser result matches a predicate.
        /// </summary>
        public Parser<T, TParseContext> When(Func<T, bool> predicate) => new When<T, TParseContext>(this, predicate);

        /// <summary>
        /// Builds a parser what returns another one based on the previous result.
        /// </summary>
        public Parser<U, TParseContext> Switch<U>(Func<ParseContext, T, Parser<U, TParseContext>> action) => new Switch<T, U, TParseContext>(this, action);

        /// <summary>
        /// Builds a parser that ensure the cursor is tat the end of the input.
        /// </summary>
        public Parser<T, TParseContext> Eof() => new Eof<T, TParseContext>(this);

        /// <summary>
        /// Builds a parser that discards the previous result and replaces it by the specified type or value.
        /// </summary>
        public Parser<U, TParseContext> Discard<U>() => new Discard<T, U, TParseContext>(this);

        /// <summary>
        /// Builds a parser that discards the previous result and replaces it by the specified type or value.
        /// </summary>
        public Parser<U, TParseContext> Discard<U>(U value) => new Discard<T, U, TParseContext>(this, value);
    }

}
