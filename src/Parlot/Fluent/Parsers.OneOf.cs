using System.Linq;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
        public static IParser<T> Or<T>(this IParser<T> parser, IParser<T> or)
        {
            if (parser is OneOf<T> oneOf)
            {
                // Return a single OneOf instance with this new one
                return new OneOf<T>(oneOf.Parsers.Append(or).ToArray());
            }
            else
            {
                return new OneOf<T>(new[] { parser, or });
            }
        }

        public static IParser Or(this IParser parser, IParser or)
        {
            if (parser is OneOf oneOf)
            {
                // Return a single OneOf instance with this new one
                return new OneOf(oneOf.Parsers.Append(or).ToArray());
            }
            else
            {
                return new OneOf(new[] { parser, or });
            }
        }

        public static IParser OneOf(params IParser[] parsers) => new OneOf(parsers);
        public static IParser<T> OneOf<T>(params IParser<T>[] parsers) => new OneOf<T>(parsers);
    }
}
