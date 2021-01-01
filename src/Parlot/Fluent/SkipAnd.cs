using System;

namespace Parlot.Fluent
{
    public sealed class SkipAnd<T> : Parser<T>
    {
        internal readonly IParser _parser1;
        internal readonly IParser<T> _parser2;
        public SkipAnd(IParser parser1, IParser<T> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var parseResult1 = new ParseResult<object>();

            if (_parser1.Parse(context, ref parseResult1))
            {
                var parseResult2 = new ParseResult<T>();

                if (_parser2.Parse(context, ref parseResult2))
                {
                    result.Set(parseResult1.Buffer, parseResult1.Start, parseResult2.End, parseResult2.Value);
                    return true;
                }
            }

            return false;
        }
    }
}
