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

            if (!parser.Parse(scanner, result))
            {
                result.SetValue(default);
            }

            return true;
        }
    }
}
