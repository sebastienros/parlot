using System.Collections.Generic;
using System.Linq;

namespace Parlot.SourceGeneration;

/// <summary>
/// Tracks deferred parsers that should materialize as separate helper methods in generated code.
/// </summary>
public sealed class DeferredRegistry
{
    private readonly Dictionary<object, (string MethodName, string? ParserName)> _methods = new();

    public string GetOrCreateMethodName(object parser, string suggestedName)
    {
        if (!_methods.TryGetValue(parser, out var entry))
        {
            var methodName = $"{suggestedName}_{_methods.Count}";
            
            // Try to get the parser's Name property via reflection
            string? parserName = null;
            var nameProp = parser.GetType().GetProperty("Name");
            if (nameProp != null && nameProp.PropertyType == typeof(string))
            {
                parserName = nameProp.GetValue(parser) as string;
            }
            
            entry = (methodName, parserName);
            _methods[parser] = entry;
        }

        return entry.MethodName;
    }

    public IEnumerable<(object Parser, string MethodName, string? ParserName)> Enumerate() =>
        _methods.Select(static kvp => (kvp.Key, kvp.Value.MethodName, kvp.Value.ParserName));
}
