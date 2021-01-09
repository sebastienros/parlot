using System;

namespace Parlot.Fluent
{
    public sealed class ZeroOrOne<T> : Parser<T>
    {
        private readonly Parser<T> _parser;

        public ZeroOrOne(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            _parser.Parse(context, ref result);

            return true;
        }
    }
}
