using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public sealed class ZeroOrMany<T, TParseContext> : Parser<List<T>, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<T, TParseContext> _parser;
        public ZeroOrMany(IParser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<List<T>> result)
        {
            context.EnterParser(this);

            var results = new List<T>();

            var start = 0;
            var end = 0;

            var first = true;
            var parsed = new ParseResult<T>();

            while (_parser.Parse(context, ref parsed))
            {
                if (first)
                {
                    first = false;
                    start = parsed.Start;
                }

                end = parsed.End;
                results.Add(parsed.Value);
            }

            result = new ParseResult<List<T>>(start, end, results);
            return true;
        }
    }
}
