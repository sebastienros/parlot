using System;
using System.Linq;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        public static OneOf<T> Or<T>(this IParser<T> parser, IParser<T> or)
        {
            if (parser is OneOf<T> oneOf)
            {
                // Return a single OneOf instance with this new one
                return new OneOf<T>(oneOf.Parsers.Concat(new[] { or }).ToArray());
            }
            else
            {
                return new OneOf<T>(new[] { parser, or });
            }
        }

        public static OneOf Or(this IParser parser, IParser or)
        {
            if (parser is OneOf oneOf)
            {
                // Return a single OneOf instance with this new one
                return new OneOf(oneOf.Parsers.Concat(new[] { or }).ToArray());
            }
            else
            {
                return new OneOf(new[] { parser, or });
            }
        }

        public static OneOf OneOf(params IParser[] parsers) => new(parsers);
        public static OneOf<T> OneOf<T>(params IParser<T>[] parsers) => new(parsers);
    }
}
