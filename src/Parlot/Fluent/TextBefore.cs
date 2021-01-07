namespace Parlot.Fluent
{
    public sealed class TextBefore<T> : Parser<TextSpan>
    {
        private readonly IParser<T> _delimiter;
        private readonly bool _canBeEmpty;
        private readonly bool _failOnEof;
        private readonly bool _consumeDelimiter;

        public TextBefore(IParser<T> delimiter, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false)
        {
            _delimiter = delimiter;
            _canBeEmpty = canBeEmpty;
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

            // Is there any text before the expected token?
            if (_delimiter.Parse(context, ref parsed))
            {
                if (!_consumeDelimiter)
                {
                    context.Scanner.Cursor.ResetPosition(start);
                }

                return _canBeEmpty;
            }

            while (true)
            {
                var previous = context.Scanner.Cursor.Position;

                var delimiterFound = _delimiter.Parse(context, ref parsed);

                if (delimiterFound || (!_failOnEof && context.Scanner.Cursor.Eof))
                {
                    var end = (!delimiterFound && context.Scanner.Cursor.Eof) ? context.Scanner.Cursor.Position : previous;

                    var length = end - start;

                    if (length == 0)
                    {
                        return _canBeEmpty;
                    }

                    if (!_consumeDelimiter)
                    {
                        context.Scanner.Cursor.ResetPosition(end);
                    }

                    result.Set(start.Offset, end.Offset, new TextSpan(context.Scanner.Buffer, start.Offset, length));
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
        private readonly bool _canBeEmpty;
        private readonly bool _failOnEof;
        private readonly bool _consumeDelimiter;

        public TextBefore(IParser delimiter, bool canBeEmpty, bool failOnEof = false, bool consumeDelimiter = false)
        {
            _delimiter = delimiter;
            _canBeEmpty = canBeEmpty;
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

            // Is there any text before the expected token?
            if (_delimiter.Parse(context, ref parsed))
            {
                if (!_consumeDelimiter)
                {
                    context.Scanner.Cursor.ResetPosition(start);
                }

                return _canBeEmpty;
            }

            while (true)
            {
                var previous = context.Scanner.Cursor.Position;

                var delimiterFound = _delimiter.Parse(context, ref parsed);

                if (delimiterFound || (!_failOnEof && context.Scanner.Cursor.Eof))
                {
                    var end = (!delimiterFound && context.Scanner.Cursor.Eof) ? context.Scanner.Cursor.Position : previous;

                    var length = end - start;

                    if (length == 0)
                    {
                        return _canBeEmpty;
                    }

                    if (!_consumeDelimiter)
                    {
                        context.Scanner.Cursor.ResetPosition(end);
                    }

                    result.Set(start.Offset, end.Offset, new TextSpan(context.Scanner.Buffer, start.Offset, length));
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
