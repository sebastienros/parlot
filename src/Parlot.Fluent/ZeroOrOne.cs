namespace Parlot.Fluent
{
    public class ZeroOrOne<T> : Parser<T>
    {
        private readonly IParser<T> parser;
        private readonly bool _skipWhitespace;

        public ZeroOrOne(IParser<T> parser, bool skipWhitespace = true)
        {
            this.parser = parser;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, IParseResult<T> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            var parsed = result != null ? new ParseResult<T>() : null;

            var start = TextPosition.Start;
            var end = TextPosition.Start;

            if (parser.Parse(scanner, parsed))
            {
                start = parsed.Start;
                end = parsed.End;

                parsed = result != null ? new ParseResult<T>() : null;

                result?.Succeed(scanner.Buffer, start, end, parsed.GetValue());
            }
            else
            {
                result?.Succeed(scanner.Buffer, start, end, default);
            }

            return true;
        }
    }
}
