using System;

namespace Parlot.Fluent
{
    public sealed class AndSkip<T, U, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<T, TParseContext> _parser1;
        private readonly IParser<U, TParseContext> _parser2;

        public AndSkip(IParser<T, TParseContext> parser1, IParser<U, TParseContext> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            if (_parser1.Parse(context, ref result))
            {
                ParseResult<U> _ = new();
                if (_parser2.Parse(context, ref _))
                {
                    return true;
                }

                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }
    }
}
