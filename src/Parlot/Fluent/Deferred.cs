using System;

namespace Parlot.Fluent
{
    public sealed class Deferred<T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        public IParser<T, TParseContext> Parser { get; set; }

        public Deferred()
        {
        }

        public Deferred(Func<Deferred<T, TParseContext>, IParser<T, TParseContext>> parser)
        {
            Parser = parser(this);
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            return Parser.Parse(context, ref result);
        }
    }
}
