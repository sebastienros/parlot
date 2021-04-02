using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public sealed class Separated<U, T> : Parser<List<T>>
    {
        private readonly Parser<U> _separator;
        private readonly Parser<T> _parser;

        private readonly bool _separatorIsChar;
        private readonly char _separatorChar;
        private readonly bool _separatorWhiteSpace;
        private readonly bool _consumeDelimiter;

        public Separated(Parser<U> separator, Parser<T> parser, bool consumeDelimiter = true)
        {
            _separator = separator ?? throw new ArgumentNullException(nameof(separator));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _consumeDelimiter = consumeDelimiter;

            // TODO: more optimization could be done for other literals by creating different implementations of this class instead of doing 
            // ifs in the Parse method. Then the builders could check the kind of literal used and return the correct implementation.

            if (separator is CharLiteral literal)
            {
                _separatorIsChar = true;
                _separatorChar = literal.Char;
                _separatorWhiteSpace = literal.SkipWhiteSpace;
            }
        }

        public override bool Parse(ParseContext context, ref ParseResult<List<T>> result)
        {
            context.EnterParser(this);

            List<T> results = null;

            var start = 0;
            var end = 0;

            var first = true;
            var parsed = new ParseResult<T>();
            var separatorResult = new ParseResult<U>();

            while (true)
            {
                if (!_parser.Parse(context, ref parsed))
                {
                    if (!first)
                    {
                        break;
                    }

                    return false;
                }

                if (first)
                {
                    start = parsed.Start;
                }

                end = parsed.End;
                results ??= new List<T>();
                results.Add(parsed.Value);
                var position = context.Scanner.Cursor.Position;

                if (_separatorIsChar)
                {
                    if (_separatorWhiteSpace)
                    {
                        context.SkipWhiteSpace();
                    }

                    if (context.Scanner.ReadChar(_separatorChar))
                    {
                        if (!_consumeDelimiter)
                        {
                            context.Scanner.Cursor.ResetPosition(position);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else if (_separator.Parse(context, ref separatorResult))
                {
                    if (!_consumeDelimiter)
                    {
                        context.Scanner.Cursor.ResetPosition(position);
                    }
                }
                else
                {
                    break;
                }
            }

            result = new ParseResult<List<T>>(start, end, results);

            return true;
        }
    }
}
