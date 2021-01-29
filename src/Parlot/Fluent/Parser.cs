using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public interface ICompilable
    {
        CompileResult Compile(Expression parseContext);
    }

    public abstract class Parser<T> : ICompilable
    { 
        public abstract bool Parse(ParseContext context, ref ParseResult<T> result);

        public virtual CompileResult Compile(Expression parseContext)
        {
            return CompileResult.Empty;
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
    }

    public static class ParserExtensions
    {
        public static T Parse<T>(this Parser<T> parser, string text, ParseContext context = null)
        {
            context ??= new ParseContext(new Scanner(text));

            var localResult = new ParseResult<T>();

            var success = parser.Parse(context, ref localResult);

            if (success)
            {
                return localResult.Value;
            }

            return default;
        }

        public static bool TryParse<TResult>(this Parser<TResult> parser, string text, out TResult value)
        {
            return parser.TryParse(text, out value, out _);
        }

        public static bool TryParse<TResult>(this Parser<TResult> parser, string text,out TResult value, out ParseError error)
        {
            return TryParse(parser, new ParseContext(new Scanner(text)), out value, out error);
        }

        public static bool TryParse<TResult>(this Parser<TResult> parser, ParseContext context, out TResult value, out ParseError error)
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
