using System;

namespace Parlot.Fluent
{
    public sealed class Between<A, T, B> : Parser<T>
    {
        private readonly Parser<T> _parser;
        private readonly Parser<A> _before;
        private readonly Parser<B> _after;

        private readonly bool _beforeIsChar;
        private readonly char _beforeChar;
        private readonly bool _beforeSkipWhiteSpace;

        private readonly bool _afterIsChar;
        private readonly char _afterChar;
        private readonly bool _afterSkipWhiteSpace;

        public Between(Parser<A> before, Parser<T> parser, Parser<B> after)
        {
            _before = before ?? throw new ArgumentNullException(nameof(before));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _after = after ?? throw new ArgumentNullException(nameof(after));

            if (before is CharLiteral literal1)
            {
                _beforeIsChar = true;
                _beforeChar = literal1.Char;
                _beforeSkipWhiteSpace = literal1.SkipWhiteSpace;
            }

            if (after is CharLiteral literal2)
            {
                _afterIsChar = true;
                _afterChar = literal2.Char;
                _afterSkipWhiteSpace = literal2.SkipWhiteSpace;
            }
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (_beforeIsChar)
            {
                if (_beforeSkipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                if (!context.Scanner.ReadChar(_beforeChar))
                {
                    return false;
                }
            }
            else
            {
                var parsedA = new ParseResult<A>();

                if (!_before.Parse(context, ref parsedA))
                {
                    return false;
                }
            }

            if (!_parser.Parse(context, ref result))
            {
                return false;
            }            

            if (_afterIsChar)
            {
                if (_afterSkipWhiteSpace)
                {
                    context.Scanner.SkipWhiteSpace();
                }

                if (!context.Scanner.ReadChar(_afterChar))
                {
                    return false;
                }
            }
            else
            {
                var parsedB = new ParseResult<B>();

                if (!_after.Parse(context, ref parsedB))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
