using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public sealed class ZeroOrMany<T> : Parser<IList<T>>
    {
        private readonly IParser<T> _parser;
        private readonly bool _skipWhiteSpace;

        public ZeroOrMany(IParser<T> parser, bool skipWhiteSpace = true)
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

            List<T> results = null;

            var start = TextPosition.Start;
            var end = TextPosition.Start;

            var first = true;
            while (_parser.Parse(scanner, out var parsed))
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
