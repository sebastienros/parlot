using System;
using System.Buffers;
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

        public override bool Parse(Scanner scanner, out ParseResult<IList<T>> result)
        {
            List<T> results = null;

            var start = TextPosition.Start;
            var end = TextPosition.Start;

            var first = true;
            result = ParseResult<IList<T>>.Empty;

            while (true)
            {
                if (!_parser.Parse(scanner, out var parsed))
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

                results.Add(parsed.GetValue());

                if (_separatorIsChar)
                {
                    if (_separatorWhiteSpace)
                    {
                        scanner.SkipWhiteSpace();
                    }

                    if (!scanner.ReadChar(_separatorChar))
                    {
                        break;
                    }
                }
                else if (!_separator.Parse(scanner, out _))
                {
                    break;
                }
            }

            result = new ParseResult<IList<T>>(scanner.Buffer, start, end, (IList<T>) results ?? Array.Empty<T>());
            return true;
        }
    }
}
