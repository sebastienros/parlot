using System;
using System.Collections.Generic;
using System.Linq;

namespace Parlot.SourceGeneration;

/// <summary>
/// Tracks parser helpers that should materialize as static helper methods in generated code.
/// </summary>
public sealed class ParserHelperRegistry
{
    private readonly Dictionary<(object Parser, bool DiscardResult), HelperEntry> _helpers = new();
    private readonly SourceGenerationContext? _context;
    private int _nextId;

    /// <summary>
    /// Creates a registry for value-producing parser helpers.
    /// </summary>
    public ParserHelperRegistry()
    {
    }

    internal ParserHelperRegistry(SourceGenerationContext context)
    {
        _context = context;
    }

    public (string MethodName, string ValueTypeName, SourceResult Result, string? ParserName) GetOrCreate(
        object parser,
        string suggestedName,
        string valueTypeName,
        Func<SourceResult> resultFactory)
    {
        var key = (parser, _context?.DiscardResult ?? false);
        if (!_helpers.TryGetValue(key, out var entry))
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
            _helpers[key] = entry;
        }

        return (entry.MethodName, entry.ValueTypeName, entry.Result, entry.ParserName);
    }

    public IEnumerable<(string MethodName, string ValueTypeName, SourceResult Result, string? ParserName)> Enumerate() =>
        _helpers.Values.Select(static h => (h.MethodName, h.ValueTypeName, h.Result, h.ParserName));

    private sealed record HelperEntry(string MethodName, string ValueTypeName, SourceResult Result, string? ParserName);
}
