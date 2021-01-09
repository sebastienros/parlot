using System;

namespace Parlot.Fluent
{
    public abstract class Parser<T>
    { 
        public abstract bool Parse(ParseContext context, ref ParseResult<T> result);

        public Parser<U> Then<U>(Func<T, U> conversion) => new Then<T, U>(this, conversion);
        public Parser<U> Then<U>(Func<ParseContext, T, U> conversion) => new Then<T, U>(this, conversion);
        public Parser<U> Else<U>(Func<T, U> conversion) => new Else<T, U>(this, conversion);
        public Parser<T> ElseError(string message) => new ElseError<T>(this, message);
        public Parser<T> Error(string message) => new Error<T>(this, message);
        public Parser<U> Error<U>(string message) => new Error<T, U>(this, message);
        public Parser<T> When(Func<T, bool> predicate) => new When<T>(this, predicate);
        public Parser<U> Switch<U>(Func<ParseContext, T, Parser<U>> action) => new Switch<T, U>(this, action);
    }

    public static class ParserExtensions
    {
        public static T Parse<T>(this Parser<T> parser, string text)
        {
            var context = new ParseContext(new Scanner(text));

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

        public static bool TryParse<TResult>(this Parser<TResult> parser, string text, out TResult value, out ParseError error)
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
