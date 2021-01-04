using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public sealed class ZeroOrMany : Parser<List<ParseResult<object>>>
    {
        private readonly IParser _parser;
        public ZeroOrMany(IParser parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<List<ParseResult<object>>> result)
        {
            context.EnterParser(this);

            var results = new List<ParseResult<object>>();

            var start = TextPosition.Start;
            var end = TextPosition.Start;

            var first = true;
            var parsed = new ParseResult<object>();

            while (_parser.Parse(context, ref parsed))
            {
                if (first)
                {
                    start = parsed.Start;
                }

                end = parsed.End;
                results.Add(parsed);
            }

            result = new ParseResult<List<ParseResult<object>>>(context.Scanner.Buffer, start, end, _parser.Name, results);
            return true;
        }
    }

    public sealed class ZeroOrMany<T> : Parser<List<T>>
    {
        private readonly IParser<T> _parser;
        public ZeroOrMany(IParser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<List<T>> result)
        {
            context.EnterParser(this);

            var results = new List<T>();

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
                results.Add(parsed.Value);
            }

            result = new ParseResult<List<T>>(context.Scanner.Buffer, start, end, _parser.Name, results);
            return true;
        }
    }
}
