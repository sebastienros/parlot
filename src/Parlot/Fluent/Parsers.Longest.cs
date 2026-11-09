using System;

namespace Parlot.Fluent;

public static partial class Parsers
{
    /// <summary>
    /// Builds a choice which invokes each of the <paramref name="parsers"/> at the same position and continues after the
    /// longest match, the first one winning when several end at the same offset.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="OneOf{T}(Parser{T}[])"/>, which stops at the first alternative that matches, the alternatives
    /// after a match are invoked too, so the result doesn't depend on their order but each one costs a parse.
    /// </remarks>
    public static Parser<T> Longest<T>(params Parser<T>[] parsers) => new Longest<T>(parsers);

    /// <summary>
    /// Builds a choice which invokes each of the <paramref name="parsers"/> at the same position and continues after the
    /// match that <paramref name="selector"/> keeps.
    /// </summary>
    /// <param name="selector">
    /// Receives the alternatives which matched, in their order, and returns the index of the match to keep, or a negative
    /// value to fail. It is only invoked when an alternative matches. <see cref="LongestMatches{T}.IndexOfLongest"/> is the
    /// match a choice without a selector keeps.
    /// </param>
    /// <param name="parsers">The alternatives.</param>
    public static Parser<T> Longest<T>(Func<LongestMatches<T>, int> selector, params Parser<T>[] parsers) => new Longest<T>(selector, parsers);

    /// <summary>
    /// Builds a choice which invokes each of the <paramref name="parsers"/> at the same position and continues after the
    /// match that <paramref name="selector"/> keeps.
    /// </summary>
    /// <param name="selector">
    /// Receives the context and the alternatives which matched, in their order, and returns the index of the match to keep,
    /// or a negative value to fail. It is only invoked when an alternative matches.
    /// <see cref="LongestMatches{T}.IndexOfLongest"/> is the match a choice without a selector keeps.
    /// </param>
    /// <param name="parsers">The alternatives.</param>
    public static Parser<T> Longest<T>(Func<ParseContext, LongestMatches<T>, int> selector, params Parser<T>[] parsers) => new Longest<T>(selector, parsers);
}
