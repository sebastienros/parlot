using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public sealed class Separated<T> : Parser<IList<T>>
    {
        private readonly IParser _separator;
        private readonly IParser<T> _parser;

        private readonly bool _separatorIsChar;
        private readonly char _separatorChar;
        private readonly bool _separatorWhiteSpace;

        public Separated(IParser separator, IParser<T> parser)
        {
            _separator = separator ?? throw new ArgumentNullException(nameof(separator));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));

            // TODO: more optimization could be done for other literals by creating different implementations of this class instead of doing 
            // ifs in the Parse method. Then the builders could check the kind of literal used and return the correct implementation.

            if (separator is CharLiteral literal)
            {
                _separatorIsChar = true;
                _separatorChar = literal.Char;
                _separatorWhiteSpace = literal.SkipWhiteSpace;
            }
        }

        public override bool Parse(ParseContext context, ref ParseResult<IList<T>> result)
        {
            context.EnterParser(this);
            
            List<T> results = null;

            var start = TextPosition.Start;
            var end = TextPosition.Start;

            var first = true;
            var parsed = new ParseResult<T>();
            var separatorResult = new ParseResult<object>();

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

                if (_separatorIsChar)
                {
                    if (_separatorWhiteSpace)
                    {
                        context.Scanner.SkipWhiteSpace();
                    }

                    if (!context.Scanner.ReadChar(_separatorChar))
                    {
                        break;
                    }
                }
                else if (!_separator.Parse(context, ref separatorResult))
                {
                    break;
                }
            }

            result = new ParseResult<IList<T>>(context.Scanner.Buffer, start, end, (IList<T>) results ?? Array.Empty<T>());
            return true;
        }
    }
}
