using System;

namespace Parlot.Fluent
{
    public sealed class SkipAnd<A, T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        internal readonly IParser<A, TParseContext> _parser1;
        internal readonly IParser<T, TParseContext> _parser2;

        public SkipAnd(IParser<A, TParseContext> parser1, IParser<T, TParseContext> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            ParseResult<A> _ = new();
            if (_parser1.Parse(context, ref _))
            {
                var parseResult2 = new ParseResult<T>();

                if (_parser2.Parse(context, ref parseResult2))
                {
                    result.Set(start.Offset, parseResult2.End, parseResult2.Value);
                    return true;
                }

                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }
    }
}
