using System.Collections.Generic;

namespace Parlot.Fluent;

public static partial class ParserExtensions
{
    public static Parser<IReadOnlyList<T>> OneOrMany<T>(this Parser<T> parser)
        => new OneOrMany<T>(parser);

    /// <summary>Builds a parser that matches at least once, up to <paramref name="max"/> times. Zero means unlimited.</summary>
    public static Parser<IReadOnlyList<T>> OneOrMany<T>(this Parser<T> parser, int max)
        => new OneOrMany<T>(parser, max);

    public static Parser<IReadOnlyList<T>> ZeroOrMany<T>(this Parser<T> parser)
        => new ZeroOrMany<T>(parser);

    /// <summary>Builds a parser that matches up to <paramref name="max"/> times. Zero means unlimited.</summary>
    public static Parser<IReadOnlyList<T>> ZeroOrMany<T>(this Parser<T> parser, int max)
        => new ZeroOrMany<T>(parser, max);

    /// <summary>
    /// Builds a parser that returns an empty list or a list containing one match.
    /// </summary>
    public static Parser<IReadOnlyList<T>> ZeroOrOne<T>(this Parser<T> parser)
        => new ZeroOrOne<T>(parser);

    public static Parser<Option<T>> Optional<T>(this Parser<T> parser)
        => new Optional<T>(parser);
}
