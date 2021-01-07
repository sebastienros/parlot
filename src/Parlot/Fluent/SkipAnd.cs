using System;

namespace Parlot.Fluent
{
    public sealed class SkipAnd<A, T> : Parser<T>
    {
        internal readonly IParser<A> _parser1;
        internal readonly IParser<T> _parser2;

        static ParseResult<A> _parseResult1 = new ParseResult<A>();

        public SkipAnd(IParser<A> parser1, IParser<T> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            if (_parser1.Parse(context, ref _parseResult1))
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
