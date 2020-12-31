using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when not all parsers return the same type.
    /// We then return the <see cref="ParseResult{T}"/> of each parser.
    /// </summary>
    public sealed class OneOf : Parser<ParseResult<object>>
    {
        public OneOf(IList<IParser> parsers)
        {
            Parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
        }

        public IList<IParser> Parsers { get; }

        public override bool Parse(ParseContext context, ref ParseResult<ParseResult<object>> result)
        {
            if (Parsers.Count == 0)
            {
                return false;
            }

            var parsed = new ParseResult<object>();

            for (var i = 0; i < Parsers.Count; i++)
            {
                if (Parsers[i].Parse(context, ref parsed))
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
        public OneOf(IList<IParser<T>> parsers)
        {
            Parsers = parsers;
        }
        public IList<IParser<T>> Parsers { get; }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (Parsers.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < Parsers.Count; i++)
            {
                if (Parsers[i].Parse(context, ref result))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
