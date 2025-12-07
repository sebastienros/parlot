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

    public SourceGenerationContext(string parseContextName = "context", string? methodNamePrefix = null)
    {
        ParseContextName = parseContextName ?? throw new ArgumentNullException(nameof(parseContextName));
        MethodNamePrefix = methodNamePrefix ?? "";
    }

    /// <summary>
    /// Name of the <c>ParseContext</c> parameter in the generated methods.
    /// </summary>
    public string ParseContextName { get; }

    /// <summary>
    /// Name of the cached cursor variable in the generated method.
    /// This is initialized once at the start of the method and reused throughout.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static - keep as instance for API consistency
    public string CursorName => "cursor";
#pragma warning restore CA1822

    /// <summary>
    /// Name of the cached scanner variable in the generated method.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static - keep as instance for API consistency
    public string ScannerName => "scanner";
#pragma warning restore CA1822

    /// <summary>
    /// Prefix for generated lambda field names to ensure uniqueness across multiple parsers in the same class.
    /// </summary>
    public string MethodNamePrefix { get; }

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
        var valueTypeName = GetTypeName(valueType);

        var result = new SourceResult(successName, valueName, valueTypeName);

        var successInit = defaultSuccess ? "true" : "false";
        result.Locals.Add($"bool {successName} = {successInit};");

        var defaultValueExpr = defaultValueExpression ?? "default";
        result.Locals.Add($"{valueTypeName} {valueName} = {defaultValueExpr};");

        return result;
    }

    public static string GetTypeName(Type type) => TypeNameHelper.GetTypeName(type);

    public string RegisterLambda(Delegate lambda)
    {
        var id = Lambdas.Register(lambda);
        return GetLambdaFieldName(id);
    }

    /// <summary>
    /// Returns a field name for a lambda that is unique to this method context.
    /// </summary>
    public string GetLambdaFieldName(int id) => $"_{MethodNamePrefix}_lambda{id}";
}
