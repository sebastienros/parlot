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

    /// <summary>
    /// Emits a loop of <paramref name="helperName"/> which reads its first item without a builder, and the following ones
    /// in a static local function. Most lists of a grammar are empty, like the operations of an operator level after most
    /// operands: the method of the list then has no builder to set up and no exception handling, so the JIT can inline it.
    /// </summary>
    /// <remarks>
    /// Only for <see langword="string"/> entry points, where the list doesn't pin its items for a compacting stream.
    /// The value of <paramref name="result"/> is only set when there is a first item, its default value is the empty list.
    /// </remarks>
    public static void EmitFirstItemThenRest(SourceGenerationContext context, SourceResult result, string helperName, string elementTypeName, int max)
    {
        var ctx = context.ParseContextName;
        var cursor = context.CursorName;
        var previousOffset = $"previousOffset{context.NextNumber()}";
        var first = $"first{context.NextNumber()}";
        var rest = $"listRest{context.NextNumber()}";

        result.Body.Add($"var {previousOffset} = {cursor}.Offset;");
        result.Body.Add($"if ({helperName}({ctx}, out var {first}) && {cursor}.Offset != {previousOffset})");
        result.Body.Add("{");
        result.Body.Add($"    {result.ValueVariable} = {rest}({ctx}, {first});");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        // The builder releases the items of a long list from the scratch stack of the thread even when an exception is thrown.
        // The local function is a single statement, so that its return isn't taken for an early return of the list parser.
        var loop = max > 0 ? $"for (var count = 1; count < {max}; count++)" : "while (true)";

        result.Body.Add(
            $"static global::System.Collections.Generic.IReadOnlyList<{elementTypeName}> {rest}(global::Parlot.Fluent.ParseContext {ctx}, {elementTypeName} first)\n" +
            "{\n" +
            $"    var {cursor} = {ctx}.Scanner.Cursor;\n" +
            $"    var list = new global::Parlot.Fluent.ListBuilder<{elementTypeName}>();\n" +
            "    list.Add(first);\n" +
            "    try\n" +
            "    {\n" +
            $"        {loop}\n" +
            "        {\n" +
            $"            var previousOffset = {cursor}.Offset;\n" +
            $"            if (!{helperName}({ctx}, out var item) || {cursor}.Offset == previousOffset)\n" +
            "            {\n" +
            "                break;\n" +
            "            }\n" +
            "            list.Add(item);\n" +
            "        }\n" +
            "        return list.ToArray();\n" +
            "    }\n" +
            "    finally\n" +
            "    {\n" +
            "        list.Release();\n" +
            "    }\n" +
            "}");
    }
}
