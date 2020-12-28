using System;

namespace Parlot.Fluent
{
    public interface IParser
    {
        bool Parse(Scanner scanner, out ParseResult<object> result);
    }

    public interface IParser<T> : IParser
    {
        bool Parse(Scanner scanner, out ParseResult<T> result);

        public IParser<U> Then<U>(Func<T, U> conversion) => new Then<T, U>(this, conversion);
        public IParser<T> When(Func<T, bool> predicate) => new When<T>(this, predicate);
    }

    public abstract class Parser<T> : IParser<T>
    {
        public abstract bool Parse(Scanner scanner, out ParseResult<T> result);

        bool IParser.Parse(Scanner scanner, out ParseResult<object> result)
        {
            if (Parse(scanner, out var localResult))
            {
                result = new ParseResult<object>(localResult.Buffer, localResult.Start, localResult.End, localResult.GetValue());
                return true;
            }
            else
            {
                result = ParseResult<object>.Empty;
                return false;
            }
        }

        public IParser<U> Then<U>(Func<T, U> conversion) => new Then<T, U>(this, conversion);
        public IParser<T> When(Func<T, bool> predicate) => new When<T>(this, predicate);
    }

    public static class IParserExtensions
    {
        public static bool TryParse<TResult>(this IParser<TResult> parser, string text, out TResult value)
        {
            try
            {
                var scanner = new Scanner(text);

                var success = parser.Parse(scanner, out var result);

                if (success)
                {
                    value = result.GetValue();
                    return true;
                }
            }
            catch (ParseException)
            {
                // This overload doesn't expose errors
            }

            value = default;
            return false;
        }

        public static bool TryParse<TResult>(this IParser<TResult> parser, string text, out TResult value, out ParseError error)
        {
            error = null;

            try
            {
                var scanner = new Scanner(text);

                var success = parser.Parse(scanner, out var result);

                if (success)
                {
                    value = result.GetValue();
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
