using System;
using System.Collections.Generic;
using System.ComponentModel;
using Parlot.Fluent;

namespace Parlot.SourceGeneration;

/// <summary>
/// Records the expressions of a grammar factory that build or reference each parser, such that the source generator
/// can report the findings of the grammar rules on the code that builds the parsers concerned, and fix it.
/// </summary>
/// <remarks>
/// The source generator wraps these expressions in <see cref="Track{T}(T, int)"/> in the copy of the factory it runs.
/// Generated parsers and the factories compiled into an application never call it.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ParserSites
{
    [ThreadStatic]
    private static Dictionary<object, List<int>>? _sites;

    [ThreadStatic]
    private static Dictionary<int, object>? _parsers;

    /// <summary>
    /// Forgets the parsers recorded on this thread, before and after a factory runs.
    /// </summary>
    public static void Reset()
    {
        _sites = null;
        _parsers = null;
    }

    /// <summary>
    /// Records that the expression identified by <paramref name="site"/> evaluated to <paramref name="parser"/>, and returns it.
    /// </summary>
    public static T Track<T>(T parser, int site) where T : class
    {
        if (parser is not null)
        {
            _sites ??= new Dictionary<object, List<int>>(LoopAnalysis.ReferenceComparer.Instance);
            _parsers ??= [];

            if (!_sites.TryGetValue(parser, out var sites))
            {
                sites = [];
                _sites.Add(parser, sites);
            }

            sites.Add(site);
            _parsers[site] = parser;
        }

        return parser!;
    }

    /// <summary>
    /// Gets the sites of the expressions that evaluated to <paramref name="parser"/>, in the order they were evaluated.
    /// </summary>
    public static IReadOnlyList<int> GetSites(object parser)
    {
        ThrowHelper.ThrowIfNull(parser, nameof(parser));

        return _sites is not null && _sites.TryGetValue(parser, out var sites) ? sites : [];
    }

    /// <summary>
    /// Gets the parser the expression identified by <paramref name="site"/> last evaluated to, or <see langword="null"/>.
    /// </summary>
    public static object? GetParser(int site) => _parsers is not null && _parsers.TryGetValue(site, out var parser) ? parser : null;
}
