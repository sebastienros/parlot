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

        public override bool Parse(Scanner scanner, out ParseResult<T> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            parser.Parse(scanner, out result);

            return true;
        }
    }
}
