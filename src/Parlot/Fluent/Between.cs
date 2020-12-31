using System;

namespace Parlot.Fluent
{
    public sealed class Between<T> : Parser<T>
    {
        private readonly IParser<T> _parser;
        private readonly IParser _before;
        private readonly IParser _after;

        private readonly bool _beforeIsChar;
        private readonly char _beforeChar;
        private readonly bool _beforeSkipWhiteSpace;

        private readonly bool _afterIsChar;
        private readonly char _afterChar;
        private readonly bool _afterSkipWhiteSpace;

        public Between(IParser before, IParser<T> parser, IParser after)
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

            var parsed = new ParseResult<object>();

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
                if (!_before.Parse(context, ref parsed))
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
                if (!_after.Parse(context, ref parsed))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
