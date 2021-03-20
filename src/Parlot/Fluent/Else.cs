using System;

namespace Parlot.Fluent
{
    public sealed class Else<T, U, TParseContext> : Parser<U, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Func<T, U> _action1;
        private readonly Func<TParseContext, T, U> _action2;
        private readonly Parser<T, TParseContext> _parser;

        public Else(Parser<T, TParseContext> parser, Func<T, U> action)
        {
            _action1 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Else(Parser<T, TParseContext> parser, Func<TParseContext, T, U> action)
        {
            _action2 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<U> result)
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

    public sealed class ElseError<T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<T, TParseContext> _parser;
        private readonly string _message;

        public ElseError(Parser<T, TParseContext> parser, string message)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _message = message;
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (!_parser.Parse(context, ref result))
            {
                throw new ParseException(_message, context.Scanner.Cursor.Position);
            }

            return true;
        }
    }

    public sealed class Error<T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<T, TParseContext> _parser;
        private readonly string _message;

        public Error(Parser<T, TParseContext> parser, string message)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _message = message;
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (_parser.Parse(context, ref result))
            {
                throw new ParseException(_message, context.Scanner.Cursor.Position);
            }

            return true;
        }
    }

    public sealed class Error<T, U, TParseContext> : Parser<U, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<T, TParseContext> _parser;
        private readonly string _message;

        public Error(Parser<T, TParseContext> parser, string message)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _message = message;
        }

        public override bool Parse(TParseContext context, ref ParseResult<U> result)
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
