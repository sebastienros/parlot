using System;

namespace Parlot.Fluent
{
    public interface IParser<T, TParseContext>
    {
        bool Parse(TParseContext context, ref ParseResult<T> result);
    }

    public abstract class Parser<T, TParseContext> : IParser<T, TParseContext>
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
        /// Builds a parser that converts the previous result when it fails.
        /// </summary>
        public Parser<U, TParseContext> Else<U>(Func<T, U> conversion) => new Else<T, U, TParseContext>(this, conversion);

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
    }

    public static class ParserExtensions
    {
        public static T Parse<T, TParseContext>(this IParser<T, TParseContext> parser, string text, TParseContext context)
        where TParseContext : ParseContext
        {
            var localResult = new ParseResult<T>();

            var success = parser.Parse(context, ref localResult);

            if (success)
            {
                return localResult.Value;
            }

            return default;
        }
        public static T Parse<T>(this IParser<T, ParseContext> parser, string text)
        {
            return parser.Parse(text, new ParseContext(new Scanner(text)));
        }

        public static bool TryParse<TResult>(this IParser<TResult, ParseContext> parser, string text, out TResult value)
        {
            return parser.TryParse(text, out value, out _);
        }

        public static bool TryParse<TResult>(this IParser<TResult, ParseContext> parser, string text, out TResult value, out ParseError error)
        {
            return TryParse(parser, new ParseContext(new Scanner(text)), out value, out error);
        }

        public static bool TryParse<TResult, TParseContext>(this IParser<TResult, TParseContext> parser, TParseContext context, out TResult value)
        where TParseContext : ParseContext
        {
            return parser.TryParse(context, out value, out _);
        }

        public static bool TryParse<TResult, TParseContext>(this IParser<TResult, TParseContext> parser, TParseContext context, out TResult value, out ParseError error)
        where TParseContext : ParseContext
        {
            error = null;

            try
            {
                var localResult = new ParseResult<TResult>();

                var success = parser.Parse(context, ref localResult);

                if (success)
                {
                    value = localResult.Value;
                    return true;
                }
            }
            catch (ParseException e)
            {
                error = new ParseError
                {
                    Message = e.Message,
                    Position = e.Position
                };
            }

            value = default;
            return false;
        }
    }
}
