using System;

namespace Parlot.Fluent
{
    public sealed class ZeroOrOne<T> : Parser<T>
    {
        private readonly IParser<T> _parser;
        private readonly bool _skipWhiteSpace;

        public ZeroOrOne(IParser<T> parser, bool skipWhiteSpace = true)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<T> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            _parser.Parse(scanner, out result);

            return true;
        }
    }
}
