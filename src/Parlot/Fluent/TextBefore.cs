namespace Parlot.Fluent
{
    public sealed class TextBefore<T> : Parser<TextSpan>
    {
        private readonly IParser<T> _delimiter;
        private readonly bool _failOnEof;
        private readonly bool _consumeDelimiter;

        public TextBefore(IParser<T> delimiter, bool failOnEof = false, bool consumeDelimiter = false)
        {
            _delimiter = delimiter;
            _failOnEof = failOnEof;
            _consumeDelimiter = consumeDelimiter;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            if (context.Scanner.Cursor.Eof)
            {
                return false;
            }

            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            var parsed = new ParseResult<T>();

            if (_delimiter.Parse(context, ref parsed))
            {
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            while (true)
            {
                if (_delimiter.Parse(context, ref parsed) || (!_failOnEof && context.Scanner.Cursor.Eof))
                {
                    var end = context.Scanner.Cursor.Eof ? context.Scanner.Cursor.Position : parsed.Start;

                    var length = end - start;

                    if (length == 0)
                    {
                        return false;
                    }

                    if (!_consumeDelimiter)
                    {
                        context.Scanner.Cursor.ResetPosition(end);
                    }

                    result.Set(context.Scanner.Buffer, start, end, Name, new TextSpan(context.Scanner.Buffer, start.Offset, length));
                    return true;
                }

                context.Scanner.Cursor.Advance();

                if (context.Scanner.Cursor.Eof)
                {
                    if (_failOnEof)
                    {
                        context.Scanner.Cursor.ResetPosition(start);
                        return false;
                    }
                }
            }
        }
    }

    public sealed class TextBefore : Parser<TextSpan>
    {
        private readonly IParser _delimiter;
        private readonly bool _failOnEof;
        private readonly bool _consumeDelimiter;

        public TextBefore(IParser delimiter, bool failOnEof = false, bool consumeDelimiter = false)
        {
            _delimiter = delimiter;
            _failOnEof = failOnEof;
            _consumeDelimiter = consumeDelimiter;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            if (context.Scanner.Cursor.Eof)
            {
                return false;
            }

            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            var parsed = new ParseResult<object>();

            if (_delimiter.Parse(context, ref parsed))
            {
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            while (true)
            {
                if (_delimiter.Parse(context, ref parsed) || (!_failOnEof && context.Scanner.Cursor.Eof))
                {
                    var end = context.Scanner.Cursor.Eof ? context.Scanner.Cursor.Position : parsed.Start;

                    var length = end - start;

                    if (length == 0)
                    {
                        return false;
                    }

                    if (!_consumeDelimiter)
                    {
                        context.Scanner.Cursor.ResetPosition(end);
                    }

                    result.Set(context.Scanner.Buffer, start, end, Name, new TextSpan(context.Scanner.Buffer, start.Offset, length));
                    return true;
                }

                context.Scanner.Cursor.Advance();

                if (context.Scanner.Cursor.Eof)
                {
                    if (_failOnEof)
                    {
                        context.Scanner.Cursor.ResetPosition(start);
                        return false;
                    }
                }
            }
        }
    }
}
