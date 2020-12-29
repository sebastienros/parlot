using System;

namespace Parlot.Fluent
{
    public interface IParser
    {
        bool Parse(Scanner scanner, ref ParseResult<object> result);
    }

    public interface IParser<T> : IParser
    {
        bool Parse(Scanner scanner, ref ParseResult<T> result);

        public IParser<U> Then<U>(Func<T, U> conversion) => new Then<T, U>(this, conversion);
        public IParser<T> When(Func<T, bool> predicate) => new When<T>(this, predicate);
        public IParser<U> Cast<U>() where U : T => Then(t => (U) t) ;
    }

    public abstract class Parser<T> : IParser<T>
    {
        public abstract bool Parse(Scanner scanner, ref ParseResult<T> result);

        bool IParser.Parse(Scanner scanner, ref ParseResult<object> result)
        {
            var localResult = new ParseResult<T>();

            if (Parse(scanner, ref localResult))
            {
                result = new ParseResult<object>(localResult.Buffer, localResult.Start, localResult.End, localResult.Value);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public static class IParserExtensions
    {
        public static bool TryParse<TResult>(this IParser<TResult> parser, string text, out TResult value)
        {
            return parser.TryParse(text, out value, out _);
        }

        public static bool TryParse<TResult>(this IParser<TResult> parser, string text, out TResult value, out ParseError error)
        {
            error = null;

            try
            {
                var scanner = new Scanner(text);

                var localResult = new ParseResult<TResult>();

                var success = parser.Parse(scanner, ref localResult);

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
