namespace Parlot.Fluent
{
    /// <summary>
    /// Doesn't parse anything and return the default value.
    /// </summary>
    public sealed class Discard<T, U> : Parser<U>
    {
        private readonly Parser<T> _parser;
        private readonly U _value;

        public Discard(Parser<T> parser)
        {
            _value = default(U);
            _parser = parser;
        }

        public Discard(Parser<T> parser, U value)
        {
            _parser = parser;
            _value = value;
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
        {
            context.EnterParser(this);

            var parsed = new ParseResult<T>();

            if (_parser.Parse(context, ref parsed))
            {
                result.Set(parsed.Start, parsed.End, _value);
                return true;
            }

            return false;
        }
    }
}
