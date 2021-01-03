namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when not all parsers return the same type.
    /// We then return the <see cref="ParseResult{T}"/> of each parser.
    /// </summary>
    public sealed class OneOf : Parser<object>
    {
        private readonly IParser[] _parsers;

        public OneOf(IParser[] parsers)
        {
            _parsers = parsers;
        }

        public IParser[] Parsers => _parsers;

        public override bool Parse(ParseContext context, ref ParseResult<object> result)
        {
            if (_parsers.Length == 0)
            {
                return false;
            }

            var start = context.Scanner.Cursor.Position;

            foreach (var parser in _parsers)
            {
                if (parser.Parse(context, ref result))
                {
                    return true;
                }

                // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }
    }

    /// <summary>
    /// OneOf the inner choices when all parsers return the same type.
    /// We then return the actual result of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class OneOf<T> : Parser<T>
    {
        private readonly IParser<T>[] _parsers;

        public OneOf(IParser<T>[] parsers)
        {
            _parsers = parsers;
        }

        public IParser<T>[] Parsers => _parsers;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (Parsers.Length == 0)
            {
                return false;
            }

            var start = context.Scanner.Cursor.Position;

            foreach (var parser in _parsers)
            {
                if (parser.Parse(context, ref result))
                {
                    return true;
                }

                // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }
    }
}
