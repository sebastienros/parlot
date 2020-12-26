using System.Collections.Generic;

namespace Parlot.Fluent
{
    public class OneOrMany<T> : Parser<IList<T>>
    {
        private readonly IParser<T> parser;
        private readonly bool _skipWhitespace;

        public OneOrMany(IParser<T> parser, bool skipWhitespace = true)
        {
            this.parser = parser;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<IList<T>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            if (!parser.Parse(scanner, out var parsed))
            {
                result = ParseResult<IList<T>>.Empty;
                return false;
            }

            var start = parsed.Start;
            var results = new List<T>();

            TextPosition end;

            do
            {
                end = parsed.End;
                results.Add(parsed.GetValue());

            } while (parser.Parse(scanner, out parsed));

            result = new ParseResult<IList<T>>(scanner.Buffer, start, end, results);
            return true;
        }
    }
}
