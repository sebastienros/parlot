using System.Collections.Generic;
using System.Linq;

namespace Parlot.SourceGeneration;

/// <summary>
/// Tracks deferred parsers that should materialize as separate helper methods in generated code.
/// </summary>
public sealed class DeferredRegistry
{
    private readonly Dictionary<object, string> _methods = new();

    public string GetOrCreateMethodName(object parser, string suggestedName)
    {
        if (!_methods.TryGetValue(parser, out var name))
        {
            name = $"{suggestedName}_{_methods.Count}";
            _methods[parser] = name;
        }

        return name;
    }

    public IEnumerable<(object Parser, string MethodName)> Enumerate() =>
        _methods.Select(static kvp => (kvp.Key, kvp.Value));
}
