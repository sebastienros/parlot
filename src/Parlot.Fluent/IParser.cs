namespace Parlot.Fluent
{
    public interface IParser<TResult>
    {
        bool Parse(Scanner scanner, IParseResult<TResult> result);
    }

    public static class IParserExtensions
    {
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
                value = result.Value;

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
