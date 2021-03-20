namespace Parlot.Fluent
{
    public sealed class Capture<T, TParseContext> : Parser<TextSpan, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<T, TParseContext> _parser;

        public Capture(IParser<T, TParseContext> parser)
        {
            _parser = parser;
        }

        public override bool Parse(TParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            ParseResult<T> _ = new();

            // Did parser succeed.
            if (_parser.Parse(context, ref _))
            {
                var end = context.Scanner.Cursor.Offset;
                var length = end - start.Offset;

                result.Set(start.Offset, end, new TextSpan(context.Scanner.Buffer, start.Offset, length));

                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }
    }
}
