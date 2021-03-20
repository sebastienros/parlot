using System;

namespace Parlot.Fluent
{
    public sealed class Not<T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<T, TParseContext> _parser;

        public Not(IParser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            if (!_parser.Parse(context, ref result))
            {
                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);
            return false;
        }
    }
}
