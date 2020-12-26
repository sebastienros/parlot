namespace Parlot.Fluent
{
    public interface IParser
    {
        bool Parse(Scanner scanner, IParseResult result);
    }

    public interface IParser<T> : IParser
    {
        bool Parse(Scanner scanner, IParseResult<T> result);
    }

    public abstract class Parser<T> : IParser<T>
    {
        public abstract bool Parse(Scanner scanner, IParseResult<T> result);

        public bool Parse(Scanner scanner, IParseResult result)
        {
            var localResult = result != null ? new ParseResult<T>() : null;

            if (Parse(scanner, localResult))
            {
                result?.Succeed(localResult.Buffer, localResult.Start, localResult.End, localResult.GetValue());
                return true;
            }
            else
            {
                result?.Fail();
                return false;
            }
        }
    }

    public static class IParserExtensions
    {
        public static IParseResult<TResult> Parse<TResult>(this IParser parser, string text)
        {
            var scanner = new Scanner(text);

            var result = new ParseResult<TResult>();

            parser.Parse(scanner, result);

            return result;
        }

        public static IParseResult<TResult> Parse<TResult>(this IParser<TResult> parser, string text)
        {
            var scanner = new Scanner(text);

            var result = new ParseResult<TResult>();

            parser.Parse(scanner, result);

            return result;
        }

        public static bool TryParse<TResult>(this IParser<TResult> parser, string text, out TResult value)
        {
            try
            {
                var result = parser.Parse(text);
                value = result.GetValue();

                return result.Success;
            }
            catch (ParseException)
            {
                // TODO: report parse errors
            }

            value = default;
            return false;
        }
    }
}
