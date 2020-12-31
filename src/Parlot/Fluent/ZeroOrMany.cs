using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public sealed class ZeroOrMany<T> : Parser<IList<T>>
    {
        private readonly IParser<T> _parser;
        public ZeroOrMany(IParser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<IList<T>> result)
        {
            context.EnterParser(this);

            List<T> results = null;

            var start = TextPosition.Start;
            var end = TextPosition.Start;

            var first = true;
            var parsed = new ParseResult<T>();

            while (_parser.Parse(context, ref parsed))
            {
                if (first)
                {
                    start = parsed.Start;
                }

                end = parsed.End;
                results ??= new List<T>();
                results.Add(parsed.Value);
            }

            result = new ParseResult<IList<T>>(context.Scanner.Buffer, start, end, (IList<T>) results ?? Array.Empty<T>());
            return true;
        }
    }
}
