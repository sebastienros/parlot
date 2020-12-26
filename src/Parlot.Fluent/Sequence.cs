namespace Parlot.Fluent
{
    public class Sequence<TInput> : IParser<IParseResult<TInput>[]>
    {
        private readonly IParser<TInput>[] _parsers;
        private readonly bool _skipWhitespace;

        public Sequence(IParser<TInput>[] parsers, bool skipWhitespace = true)
        {
            _parsers = parsers;
            _skipWhitespace = skipWhitespace;
        }

        public bool Parse(Scanner scanner, IParseResult<IParseResult<TInput>[]> result)
        {
            var results = new ParseResult<TInput>[_parsers.Length];

            if (_parsers.Length == 0)
            {
                return true;
            }

            var success = true;
        
            for (var i = 0; i < _parsers.Length; i++)
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                var parsed = result != null ? new ParseResult<TInput>() : null;

                if (!_parsers[i].Parse(scanner, parsed))
                {
                    success = false;
                    break;
                }

                if (parsed != null)
                {
                    results[i] = parsed;
                }
            }

            if (success)
            {
                result?.Succeed(results[0].Buffer, results[0].Start, results[^1].End, results);
                return true;
            }
            else
            {
                result?.Fail();
                return false;
            }
        }
    }
}
