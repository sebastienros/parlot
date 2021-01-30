using System;

namespace Parlot.Fluent
{
    public sealed class Else<T, U> : Parser<U>
    {
        private readonly Func<T, U>? _action1;
        private readonly Func<ParseContext, T, U>? _action2;
        private readonly Parser<T> _parser;

        public Else(Parser<T> parser, Func<T, U> action)
        {
            _action1 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Else(Parser<T> parser, Func<ParseContext, T, U> action)
        {
            _action2 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
        {
            context.EnterParser(this);
            
            var parsed = new ParseResult<T>();

            if (!_parser.Parse(context, ref parsed))
            {
                if (_action1 != null)
                {
                    var value = _action1.Invoke(parsed.Value);
                    result.Set(parsed.Start, parsed.End, value);
                }

                if (_action2 != null)
                {
                    var value = _action2.Invoke(context, parsed.Value);
                    result.Set(parsed.Start, parsed.End, value);
                }

                return true;
            }

            return false;
        }
    }

    public sealed class ElseError<T> : Parser<T>
    {
        private readonly Parser<T> _parser;
        private readonly string _message;

        public ElseError(Parser<T> parser, string message)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _message = message;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (!_parser.Parse(context, ref result))
            {
                throw new ParseException(_message, context.Scanner.Cursor.Position);
            }

            return true;
        }
    }

    public sealed class Error<T> : Parser<T>
    {
        private readonly Parser<T> _parser;
        private readonly string _message;

        public Error(Parser<T> parser, string message)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _message = message;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (_parser.Parse(context, ref result))
            {
                throw new ParseException(_message, context.Scanner.Cursor.Position);
            }

            return true;
        }
    }

    public sealed class Error<T, U> : Parser<U>
    {
        private readonly Parser<T> _parser;
        private readonly string _message;

        public Error(Parser<T> parser, string message)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _message = message;
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
        {
            context.EnterParser(this);

            var parsed = new ParseResult<T>();

            if (_parser.Parse(context, ref parsed))
            {
                throw new ParseException(_message, context.Scanner.Cursor.Position);
            }

            return true;
        }
    }
}
