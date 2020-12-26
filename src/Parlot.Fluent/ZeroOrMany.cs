using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public class ZeroOrMany<T> : Parser<IList<T>>
    {
        private readonly IParser<T> parser;
        private readonly bool _skipWhitespace;

        public ZeroOrMany(IParser<T> parser, bool skipWhitespace = true)
        {
            this.parser = parser;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, IParseResult<IList<T>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            var parsed = new ParseResult<T>();

            List<T> results = null;

            var start = TextPosition.Start;
            var end = TextPosition.Start;

            var first = true;
            while (parser.Parse(scanner, parsed))
            {
                if (first)
                {
                    start = parsed.Start;
                }

                end = parsed.End;

                results ??= new List<T>();
                results.Add(parsed.GetValue());

                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }
            }

            result?.Succeed(scanner.Buffer, start, end, (IList<T>) results ?? Array.Empty<T>());
            return true;
        }
    }
}
