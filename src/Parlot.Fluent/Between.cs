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

        public override bool Parse(Scanner scanner, out ParseResult<T> result)
        {
            var start = scanner.Cursor.Position;
            result = ParseResult<T>.Empty;

            if (_beforeIsChar)
            {
                if (_beforeSkipWhiteSpace)
                {
                    scanner.SkipWhiteSpace();
                }

                if (!scanner.ReadChar(_beforeChar))
                {
                    return false;
                }
            }
            else
            {
                if (!_before.Parse(scanner, out _))
                {
                    return false;
                }
            }

            if (_parser.Parse(scanner, out var parsed))
            {
                return false;
            }            

            if (_afterIsChar)
            {
                if (_afterSkipWhiteSpace)
                {
                    scanner.SkipWhiteSpace();
                }

                if (!scanner.ReadChar(_afterChar))
                {
                    return false;
                }
            }
            else
            {
                if (!_after.Parse(scanner, out _))
                {
                    return false;
                }
            }

            result = new ParseResult<T>(parsed.Buffer, start, parsed.End, parsed.GetValue());
            return true;
        }
    }
}
