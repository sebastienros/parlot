namespace Parlot.SourceGeneration;

/// <summary>
/// Emits the code of the list parsers which accumulate their items with <c>Parlot.Fluent.ListBuilder&lt;T&gt;</c>.
/// </summary>
internal static class ListBuilderSource
{
    /// <summary>
    /// Returns the statement declaring the builder <paramref name="listName"/>.
    /// </summary>
    public static string Declare(string listName, string elementTypeName)
    {
        return $"var {listName} = new global::Parlot.Fluent.ListBuilder<{elementTypeName}>();";
    }

    /// <summary>
    /// Moves the statements emitted from <paramref name="bodyStart"/> to a <see langword="try"/> block whose
    /// <see langword="finally"/> block releases the builder, such that an exception doesn't leave its items on the
    /// scratch stack of the thread.
    /// </summary>
    public static void ReleaseInFinally(SourceResult result, int bodyStart, string listName, string indent = "")
    {
        var body = result.Body;

        for (var i = bodyStart; i < body.Count; i++)
        {
            body[i] = "    " + body[i];
        }

        body.Insert(bodyStart, $"{indent}try");
        body.Insert(bodyStart + 1, $"{indent}{{");
        body.Add($"{indent}}}");
        body.Add($"{indent}finally");
        body.Add($"{indent}{{");
        body.Add($"{indent}    {listName}.Release();");
        body.Add($"{indent}}}");
    }
}
