namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when not all parsers return the same type.
    /// We then return the <see cref="IParseResult"/> of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OneOf : Parser<IParseResult>
    {
        private readonly IParser[] _parsers;
        private readonly bool _skipWhitespace;

        public OneOf(IParser[] parsers, bool skipWhitespace = true)
        {
            _parsers = parsers;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, IParseResult<IParseResult> result)
        {
            if (_parsers.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                if (_parsers[i].Parse(scanner, result))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// OneOf the inner choices when all parsers return the same type.
    /// We then return the actual result of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OneOf<T> : Parser<T>
    {
        private readonly IParser<T>[] _parsers;
        private readonly bool _skipWhitespace;

        public OneOf(IParser<T>[] parsers, bool skipWhitespace = true)
        {
            _parsers = parsers;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, IParseResult<T> result)
        {
            if (_parsers.Length == 0)
            {
                return false;
            }

            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (_parsers[i].Parse(scanner, result))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
