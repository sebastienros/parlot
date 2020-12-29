using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public sealed class OneOrMany<T> : Parser<IList<T>>
    {
        private readonly IParser<T> _parser;
        private readonly bool _skipWhiteSpace;

        public OneOrMany(IParser<T> parser, bool skipWhiteSpace = true)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, ref ParseResult<IList<T>> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var parsed = new ParseResult<T>();

            if (!_parser.Parse(scanner, ref parsed))
            {
                return false;
            }

            var start = parsed.Start;
            var results = new List<T>();

            TextPosition end;

            do
            {
                end = parsed.End;
                results.Add(parsed.Value);

            } while (_parser.Parse(scanner, ref parsed));

            result = new ParseResult<IList<T>>(scanner.Buffer, start, end, results);
            return true;
        }
    }
}
