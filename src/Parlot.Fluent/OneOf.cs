using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when not all parsers return the same type.
    /// We then return the <see cref="IParseResult"/> of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class OneOf : Parser<ParseResult<object>>
    {
        private readonly bool _skipWhiteSpace;

        public OneOf(IList<IParser> parsers, bool skipWhiteSpace = true)
        {
            Parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public IList<IParser> Parsers { get; }

        public override bool Parse(Scanner scanner, out ParseResult<ParseResult<object>> result)
        {
            if (Parsers.Count == 0)
            {
                result = ParseResult<ParseResult<object>>.Empty;
                return false;
            }

            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            for (var i = 0; i < Parsers.Count; i++)
            {
                if (Parsers[i].Parse(scanner, out var parsed))
                {
                    result = new ParseResult<ParseResult<object>>(parsed.Buffer, parsed.Start, parsed.End, parsed);
                    return true;
                }
            }

            result = ParseResult<ParseResult<object>>.Empty;
            return false;
        }

    }

    /// <summary>
    /// OneOf the inner choices when all parsers return the same type.
    /// We then return the actual result of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class OneOf<T> : Parser<T>
    {
        private readonly bool _skipWhiteSpace;

        public OneOf(IList<IParser<T>> parsers, bool skipWhiteSpace = true)
        {
            Parsers = parsers;
            _skipWhiteSpace = skipWhiteSpace;
        }
        public IList<IParser<T>> Parsers { get; }

        public override bool Parse(Scanner scanner, out ParseResult<T> result)
        {
            if (Parsers.Count == 0)
            {
                result = ParseResult<T>.Empty;
                return false;
            }

            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            for (var i = 0; i < Parsers.Count; i++)
            {
                if (Parsers[i].Parse(scanner, out result))
                {
                    return true;
                }
            }

            result = ParseResult<T>.Empty;
            return false;
        }
    }
}
