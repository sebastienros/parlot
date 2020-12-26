namespace Parlot.Fluent
{
    public class FirstOf<TInput> : IParser<IParseResult<TInput>>
    {
        private readonly IParser<TInput>[] _parsers;
        private readonly bool _skipWhitespace;

        public FirstOf(IParser<TInput>[] parsers, bool skipWhitespace = true)
        {
            _parsers = parsers;
            _skipWhitespace = skipWhitespace;
        }

        public bool Parse(Scanner scanner, IParseResult<IParseResult<TInput>> result)
        {
            if (_parsers.Length == 0)
            {
                return false;
            }

            var parsed = result != null ? new ParseResult<TInput>() : null;

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                if (_parsers[i].Parse(scanner, parsed))
                {
                    result?.Succeed(parsed.Buffer, parsed.Start, parsed.End, parsed);
                    return true;
                }
            }

            result?.Fail();
            return false;
        }
    }
}
