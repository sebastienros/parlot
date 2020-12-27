using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public class OneOrMany<T> : Parser<IList<T>>
    {
        private readonly IParser<T> _parser;
        private readonly bool _skipWhiteSpace;

        public OneOrMany(IParser<T> parser, bool skipWhiteSpace = true)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<IList<T>> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            if (!_parser.Parse(scanner, out var parsed))
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

            } while (_parser.Parse(scanner, out parsed));

            result = new ParseResult<IList<T>>(scanner.Buffer, start, end, results);
            return true;
        }
    }
}
