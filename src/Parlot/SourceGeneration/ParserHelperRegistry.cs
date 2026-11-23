using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Parlot.SourceGeneration;

/// <summary>
/// Marker for internal composite helper keys (such as token and seek-guard wrappers) that implement
/// value equality over the underlying parser reference.
/// </summary>
internal interface IHelperKey
{
}

/// <summary>
/// Tracks parser helpers that should materialize as static helper methods in generated code.
/// </summary>
public sealed class ParserHelperRegistry
{
    private readonly Dictionary<(object Parser, bool DiscardResult, bool IsCompacting), HelperEntry> _helpers = new(KeyComparer.Instance);
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
        var key = (parser, _context?.DiscardResult ?? false, _context?.IsCompacting ?? false);
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

    private sealed class KeyComparer : IEqualityComparer<(object Parser, bool DiscardResult, bool IsCompacting)>
    {
        public static readonly KeyComparer Instance = new();

        public bool Equals((object Parser, bool DiscardResult, bool IsCompacting) x, (object Parser, bool DiscardResult, bool IsCompacting) y)
        {
            if (x.DiscardResult != y.DiscardResult || x.IsCompacting != y.IsCompacting)
            {
                return false;
            }

            if (x.Parser is IHelperKey)
            {
                return x.Parser.Equals(y.Parser);
            }

            return ReferenceEquals(x.Parser, y.Parser);
        }

        public int GetHashCode((object Parser, bool DiscardResult, bool IsCompacting) obj)
        {
            var parserHash = obj.Parser is IHelperKey
                ? obj.Parser.GetHashCode()
                : RuntimeHelpers.GetHashCode(obj.Parser);
            unchecked
            {
                var hash = parserHash * 397;
                hash = (hash ^ (obj.DiscardResult ? 1 : 0)) * 397;
                return hash ^ (obj.IsCompacting ? 1 : 0);
            }
        }
    }
}
