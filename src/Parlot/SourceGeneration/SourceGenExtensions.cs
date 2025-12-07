using System.Collections.Generic;

namespace Parlot.SourceGeneration;

/// <summary>
/// Extension methods to simplify source generation code in <see cref="ISourceable"/> implementations.
/// </summary>
public static class SourceGenExtensions
{
    /// <summary>
    /// Adds a new local variable declaration and returns the variable name.
    /// </summary>
    public static string DeclareLocal(this SourceResult result, string type, string namePrefix, SourceGenerationContext context, string? initialValue = null)
    {
        var name = $"{namePrefix}{context.NextNumber()}";
        var init = initialValue ?? "default";
        result.Locals.Add($"{type} {name} = {init};");
        return name;
    }

    /// <summary>
    /// Declares a TextPosition variable for saving cursor position.
    /// </summary>
    public static string DeclarePositionVariable(this SourceResult result, SourceGenerationContext context, string prefix = "start")
    {
        var name = $"{prefix}{context.NextNumber()}";
        result.Body.Add($"var {name} = default(global::Parlot.TextPosition);");
        return name;
    }

    /// <summary>
    /// Emits code to save the current cursor position.
    /// </summary>
    public static void SavePosition(this SourceResult result, SourceGenerationContext context, string positionVarName, string indent = "")
    {
        result.Body.Add($"{indent}{positionVarName} = {context.CursorName}.Position;");
    }

    /// <summary>
    /// Emits code to reset the cursor to a saved position.
    /// </summary>
    public static void ResetPosition(this SourceResult result, SourceGenerationContext context, string positionVarName, string indent = "")
    {
        result.Body.Add($"{indent}{context.CursorName}.ResetPosition({positionVarName});");
    }

    /// <summary>
    /// Emits code to reset position if the result failed.
    /// </summary>
    public static void ResetPositionOnFailure(this SourceResult result, SourceGenerationContext context, string positionVarName, string indent = "")
    {
        result.Body.Add($"{indent}if (!{result.SuccessVariable})");
        result.Body.Add($"{indent}{{");
        result.Body.Add($"{indent}    {context.CursorName}.ResetPosition({positionVarName});");
        result.Body.Add($"{indent}}}");
    }

    /// <summary>
    /// Emits code to skip whitespace.
    /// </summary>
    public static void SkipWhiteSpace(this SourceResult result, SourceGenerationContext context, string indent = "")
    {
        result.Body.Add($"{indent}{context.ParseContextName}.SkipWhiteSpace();");
    }

    /// <summary>
    /// Emits the locals and body from an inner parser result.
    /// </summary>
    public static void EmitInnerParser(this SourceResult result, SourceResult inner, string indent = "")
    {
        foreach (var local in inner.Locals)
        {
            result.Body.Add($"{indent}{local}");
        }

        foreach (var stmt in inner.Body)
        {
            result.Body.Add($"{indent}{stmt}");
        }
    }

    /// <summary>
    /// Emits code to set success and value from an inner parser result.
    /// </summary>
    public static void PropagateSuccess(this SourceResult result, SourceResult inner, string indent = "")
    {
        result.Body.Add($"{indent}if ({inner.SuccessVariable})");
        result.Body.Add($"{indent}{{");
        result.Body.Add($"{indent}    {result.SuccessVariable} = true;");
        result.Body.Add($"{indent}    {result.ValueVariable} = {inner.ValueVariable};");
        result.Body.Add($"{indent}}}");
    }

    /// <summary>
    /// Emits code to set success and a custom value expression.
    /// </summary>
    public static void SetSuccess(this SourceResult result, string valueExpression, string indent = "")
    {
        result.Body.Add($"{indent}{result.SuccessVariable} = true;");
        result.Body.Add($"{indent}{result.ValueVariable} = {valueExpression};");
    }

    /// <summary>
    /// Emits code to set success to false.
    /// </summary>
    public static void SetFailure(this SourceResult result, string indent = "")
    {
        result.Body.Add($"{indent}{result.SuccessVariable} = false;");
    }

    /// <summary>
    /// Emits an if block checking the inner parser's success.
    /// </summary>
    public static void IfSuccess(this SourceResult result, SourceResult inner, IEnumerable<string> thenStatements, string indent = "")
    {
        result.Body.Add($"{indent}if ({inner.SuccessVariable})");
        result.Body.Add($"{indent}{{");
        foreach (var stmt in thenStatements)
        {
            result.Body.Add($"{indent}    {stmt}");
        }
        result.Body.Add($"{indent}}}");
    }

    /// <summary>
    /// Emits an if-else block checking the inner parser's success.
    /// </summary>
    public static void IfSuccessElse(this SourceResult result, SourceResult inner, IEnumerable<string> thenStatements, IEnumerable<string> elseStatements, string indent = "")
    {
        result.Body.Add($"{indent}if ({inner.SuccessVariable})");
        result.Body.Add($"{indent}{{");
        foreach (var stmt in thenStatements)
        {
            result.Body.Add($"{indent}    {stmt}");
        }
        result.Body.Add($"{indent}}}");
        result.Body.Add($"{indent}else");
        result.Body.Add($"{indent}{{");
        foreach (var stmt in elseStatements)
        {
            result.Body.Add($"{indent}    {stmt}");
        }
        result.Body.Add($"{indent}}}");
    }

    /// <summary>
    /// Emits an if block checking for success with position reset on failure.
    /// </summary>
    public static void IfSuccessElseReset(this SourceResult result, SourceResult inner, SourceGenerationContext context, string positionVarName, IEnumerable<string> thenStatements, string indent = "")
    {
        result.Body.Add($"{indent}if ({inner.SuccessVariable})");
        result.Body.Add($"{indent}{{");
        foreach (var stmt in thenStatements)
        {
            result.Body.Add($"{indent}    {stmt}");
        }
        result.Body.Add($"{indent}}}");
        result.Body.Add($"{indent}else");
        result.Body.Add($"{indent}{{");
        result.Body.Add($"{indent}    {context.CursorName}.ResetPosition({positionVarName});");
        result.Body.Add($"{indent}    {result.SuccessVariable} = false;");
        result.Body.Add($"{indent}}}");
    }
}
