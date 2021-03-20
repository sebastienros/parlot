using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public sealed class OneOrMany<T, TParseContext> : Parser<List<T>, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<T, TParseContext> _parser;

        public OneOrMany(IParser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<List<T>> result)
        {
            context.EnterParser(this);

            var parsed = new ParseResult<T>();

            if (!_parser.Parse(context, ref parsed))
            {
                return false;
            }

            var start = parsed.Start;
            var results = new List<T>();

            int end = 0;

            do
            {
                end = parsed.End;
                results.Add(parsed.Value);

            } while (_parser.Parse(context, ref parsed));

            result = new ParseResult<List<T>>(start, end, results);
            return true;
        }
    }
}
