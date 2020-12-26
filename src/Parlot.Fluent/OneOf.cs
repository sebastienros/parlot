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

            var parsed = result != null ? new ParseResult() : null;

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                if (_parsers[i].Parse(scanner, parsed))
                {
                    // TODO: this might incur boxing
                    result?.Succeed(parsed.Buffer, parsed.Start, parsed.End, parsed.GetValue());
                    return true;
                }
            }

            result?.Fail();
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

            for (var i = 0; i < _parsers.Length; i++)
            {
                var parsed = result != null ? new ParseResult<T>() : null;

                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                if (_parsers[i].Parse(scanner, parsed))
                {
                    result?.Succeed(scanner.Buffer, parsed.Start, parsed.End, parsed.GetValue());
                    return true;
                }
            }

            result?.Fail();
            return false;
        }
    }
}
