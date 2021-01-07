using System;

namespace Parlot.Fluent
{
    public sealed class AndSkip<T> : Parser<T>
    {
        internal readonly IParser<T> _parser1;
        internal readonly IParser _parser2;

        static ParseResult<object> _parseResult2 = new ParseResult<object>();

        public AndSkip(IParser<T> parser1, IParser parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            if (_parser1.Parse(context, ref result))
            {
                if (_parser2.Parse(context, ref _parseResult2))
                {
                    return true;
                }

                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }
    }
}
