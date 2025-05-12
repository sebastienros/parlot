using System.Collections.Generic;

namespace Parlot.Fluent;

public static partial class ParserExtensions
{
    public static Parser<T> Optional<T>(this Parser<T> parser, T defaultValue)
        => new ZeroOrOne<T>(parser, defaultValue);

    public static Parser<IReadOnlyList<T>> OneOrMany<T>(this Parser<T> parser)
        => new OneOrMany<T>(parser);

    public static Parser<IReadOnlyList<T>> ZeroOrMany<T>(this Parser<T> parser)
        => new ZeroOrMany<T>(parser);

// TODO: Create Optional<T>
    public static Parser<T> ZeroOrOne<T>(this Parser<T> parser, T defaultValue)
        => new ZeroOrOne<T>(parser, defaultValue);

    public static Parser<T> ZeroOrOne<T>(this Parser<T> parser)
        => new ZeroOrOne<T>(parser, default!);

}
