using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Parlot;

namespace Parlot.SourceGeneration;

/// <summary>
/// Keeps track of user-provided delegates (for example from <c>Then</c>) that must be available
/// to generated source code as fields or constructor parameters.
/// </summary>
public sealed class LambdaRegistry
{
    private readonly Dictionary<Delegate, int> _ids = new();
    private readonly Dictionary<int, string?> _sourceCode = new();

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
    /// Sets the source code for a registered lambda.
    /// </summary>
    public void SetSourceCode(int id, string sourceCode)
    {
        _sourceCode[id] = sourceCode;
    }

    /// <summary>
    /// Gets the source code for a registered lambda, or null if not available.
    /// </summary>
    public string? GetSourceCode(int id)
    {
        return _sourceCode.TryGetValue(id, out var source) ? source : null;
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

    /// <summary>
    /// Gets information about a delegate's method for source code matching.
    /// </summary>
    public static (string? TypeName, string MethodName, int MetadataToken) GetDelegateInfo(Delegate @delegate)
    {
        var method = @delegate.Method;
        return (method.DeclaringType?.FullName, method.Name, method.MetadataToken);
    }
}
