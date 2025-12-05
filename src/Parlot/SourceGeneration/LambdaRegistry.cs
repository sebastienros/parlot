using System;
using System.Collections.Generic;
using System.Linq;
using Parlot;

namespace Parlot.SourceGeneration;

/// <summary>
/// Keeps track of user-provided delegates (for example from <c>Then</c>) that must be available
/// to generated source code as fields or constructor parameters.
/// </summary>
public sealed class LambdaRegistry
{
    private readonly Dictionary<Delegate, int> _ids = new();

    /// <summary>
    /// Registers a delegate and returns its stable identifier.
    /// </summary>
    public int Register(Delegate @delegate)
    {
        ThrowHelper.ThrowIfNull(@delegate, nameof(@delegate));

        if (!_ids.TryGetValue(@delegate, out var id))
        {
            id = _ids.Count;
            _ids[@delegate] = id;
        }

        return id;
    }

    /// <summary>
    /// Returns a conventional field name for a given delegate identifier.
    /// </summary>
    public static string GetFieldName(int id) => $"_lambda{id}";

    /// <summary>
    /// Enumerates all registered delegates and their identifiers.
    /// </summary>
    public IEnumerable<(int Id, Delegate Delegate)> Enumerate() =>
        _ids.Select(static kvp => (kvp.Value, kvp.Key));
}
