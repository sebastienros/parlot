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

    public (string MethodName, string ValueTypeName, SourceResult Result, string? ParserName) GetOrCreate(
        object parser,
        string suggestedName,
        string valueTypeName,
        Func<SourceResult> resultFactory)
    {
        if (!_helpers.TryGetValue(parser, out var entry))
        {
            var methodName = suggestedName + "_" + _nextId++;
            var result = resultFactory();
            
            // Try to get the parser's Name property via reflection
            string? parserName = null;
            var nameProp = parser.GetType().GetProperty("Name");
            if (nameProp != null && nameProp.PropertyType == typeof(string))
            {
                parserName = nameProp.GetValue(parser) as string;
            }
            
            entry = new HelperEntry(methodName, valueTypeName, result, parserName);
            _helpers[parser] = entry;
        }

        return (entry.MethodName, entry.ValueTypeName, entry.Result, entry.ParserName);
    }

    public IEnumerable<(string MethodName, string ValueTypeName, SourceResult Result, string? ParserName)> Enumerate() =>
        _helpers.Values.Select(static h => (h.MethodName, h.ValueTypeName, h.Result, h.ParserName));

    private sealed record HelperEntry(string MethodName, string ValueTypeName, SourceResult Result, string? ParserName);
}
