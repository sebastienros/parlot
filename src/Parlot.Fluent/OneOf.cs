namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when not all parsers return the same type.
    /// We then return the <see cref="IParseResult"/> of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OneOf : Parser<ParseResult<object>>
    {
        private readonly IParser[] _parsers;
        private readonly bool _skipWhitespace;

        public OneOf(IParser[] parsers, bool skipWhitespace = true)
        {
            _parsers = parsers;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<ParseResult<object>> result)
        {
            if (_parsers.Length == 0)
            {
                result = ParseResult<ParseResult<object>>.Empty;
                return false;
            }

            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (_parsers[i].Parse(scanner, out var parsed))
                {
                    result = new ParseResult<ParseResult<object>>(parsed.Buffer, parsed.Start, parsed.End, parsed);
                    return true;
                }
            }

            result = ParseResult<ParseResult<object>>.Empty;
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

        public override bool Parse(Scanner scanner, out ParseResult<T> result)
        {
            if (_parsers.Length == 0)
            {
                result = ParseResult<T>.Empty;
                return false;
            }

            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (_parsers[i].Parse(scanner, out result))
                {
                    return true;
                }
            }

            result = ParseResult<T>.Empty;
            return false;
        }
    }
}
