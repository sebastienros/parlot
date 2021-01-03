using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when not all parsers return the same type.
    /// We then return the <see cref="ParseResult{T}"/> of each parser.
    /// </summary>
    public sealed class OneOf : Parser<object>
    {
        public OneOf(IList<IParser> parsers)
        {
            Parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
        }

        public IList<IParser> Parsers { get; }

        public override bool Parse(ParseContext context, ref ParseResult<object> result)
        {
            if (Parsers.Count == 0)
            {
                return false;
            }

            var start = context.Scanner.Cursor.Position;

            for (var i = 0; i < Parsers.Count; i++)
            {
                if (Parsers[i].Parse(context, ref result))
                {
                    return true;
                }

                // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
                context.Scanner.Cursor.ResetPosition(start);
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

            var start = context.Scanner.Cursor.Position;

            for (var i = 0; i < Parsers.Count; i++)
            {
                if (Parsers[i].Parse(context, ref result))
                {
                    return true;
                }

                // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }
    }
}
