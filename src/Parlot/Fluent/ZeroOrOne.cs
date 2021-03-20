using System;

namespace Parlot.Fluent
{
    public sealed class ZeroOrOne<T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<T, TParseContext> _parser;

        public ZeroOrOne(IParser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            _parser.Parse(context, ref result);

            return true;
        }
    }
}
