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

        public override bool Parse(Scanner scanner, out ParseResult<IList<T>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            List<T> results = null;

            var start = TextPosition.Start;
            var end = TextPosition.Start;

            var first = true;
            while (parser.Parse(scanner, out var parsed))
            {
                if (first)
                {
                    start = parsed.Start;
                }

                end = parsed.End;

                results ??= new List<T>();
                results.Add(parsed.GetValue());
            }

            result = new ParseResult<IList<T>>(scanner.Buffer, start, end, (IList<T>) results ?? Array.Empty<T>());
            return true;
        }
    }
}
