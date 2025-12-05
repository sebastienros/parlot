using System;
using System.Collections.Generic;
using Parlot;

namespace Parlot.SourceGeneration;

/// <summary>
/// Represents the context of a source-generation phase, coordinating all the parsers involved.
/// This is the source-based counterpart to <see cref="Parlot.Compilation.CompilationContext"/>.
/// </summary>
public sealed class SourceGenerationContext
{
    private int _number;

    public SourceGenerationContext(string parseContextName = "context")
    {
        ParseContextName = parseContextName ?? throw new ArgumentNullException(nameof(parseContextName));
    }

    /// <summary>
    /// Name of the <c>ParseContext</c> parameter in the generated methods.
    /// </summary>
    public string ParseContextName { get; }

    /// <summary>
    /// Global locals (as code lines) for the generated root method.
    /// </summary>
    public IList<string> GlobalLocals { get; } = new List<string>();

    /// <summary>
    /// Global body statements for the generated root method.
    /// </summary>
    public IList<string> GlobalBody { get; } = new List<string>();

    /// <summary>
    /// Registry of user-provided delegates used by parsers such as <c>Then</c>.
    /// </summary>
    public LambdaRegistry Lambdas { get; } = new();

    /// <summary>
    /// Registry of deferred parsers that should become separate helper methods.
    /// </summary>
    public DeferredRegistry Deferred { get; } = new();

    /// <summary>
    /// Returns a new unique number for the current compilation.
    /// </summary>
    public int NextNumber() => _number++;

    /// <summary>
    /// Creates a new <see cref="SourceResult"/> with conventional success and value names.
    /// </summary>
    public SourceResult CreateResult(Type valueType, bool defaultSuccess = false, string? defaultValueExpression = null)
    {
        ThrowHelper.ThrowIfNull(valueType, nameof(valueType));

        var successName = $"success{NextNumber()}";
        var valueName = $"value{NextNumber()}";
        var valueTypeName = valueType.FullName ?? valueType.Name;

        var result = new SourceResult(successName, valueName, valueTypeName);

        var successInit = defaultSuccess ? "true" : "false";
        result.Locals.Add($"bool {successName} = {successInit};");

        var defaultValueExpr = defaultValueExpression ?? "default";
        result.Locals.Add($"{valueTypeName} {valueName} = {defaultValueExpr};");

        return result;
    }
}
