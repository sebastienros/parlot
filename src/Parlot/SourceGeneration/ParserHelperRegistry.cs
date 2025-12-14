using System;
using System.Collections.Generic;
using System.Linq;

namespace Parlot.SourceGeneration;

/// <summary>
/// Tracks parser helpers that should materialize as static helper methods in generated code.
/// </summary>
public sealed class ParserHelperRegistry
{
    private readonly Dictionary<object, HelperEntry> _helpers = new();
    private int _nextId;

    public (string MethodName, string ValueTypeName, SourceResult Result) GetOrCreate(
        object parser,
        string suggestedName,
        string valueTypeName,
        Func<SourceResult> resultFactory)
    {
        if (!_helpers.TryGetValue(parser, out var entry))
        {
            var methodName = suggestedName + "_" + _nextId++;
            var result = resultFactory();
            entry = new HelperEntry(methodName, valueTypeName, result);
            _helpers[parser] = entry;
        }

        return (entry.MethodName, entry.ValueTypeName, entry.Result);
    }

    public IEnumerable<(string MethodName, string ValueTypeName, SourceResult Result)> Enumerate() =>
        _helpers.Values.Select(static h => (h.MethodName, h.ValueTypeName, h.Result));

    private sealed record HelperEntry(string MethodName, string ValueTypeName, SourceResult Result);
}
