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

        public override bool Parse(Scanner scanner, ref ParseResult<ParseResult<object>> result)
        {
            if (Parsers.Count == 0)
            {
                return false;
            }

            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var parsed = new ParseResult<object>();

            for (var i = 0; i < Parsers.Count; i++)
            {
                if (Parsers[i].Parse(scanner, ref parsed))
                {
                    result.Set(parsed.Buffer, parsed.Start, parsed.End, parsed);
                    return true;
                }
            }

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

        public override bool Parse(Scanner scanner, ref ParseResult<T> result)
        {
            if (Parsers.Count == 0)
            {
                return false;
            }

            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            for (var i = 0; i < Parsers.Count; i++)
            {
                if (Parsers[i].Parse(scanner, ref result))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
