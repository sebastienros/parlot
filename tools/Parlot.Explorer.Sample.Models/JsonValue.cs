using System.Collections.Generic;

namespace Parlot.Explorer.Sample;

// Counts are retained even when the explorer's bounded result snapshot omits large subtrees.
public sealed record JsonValue(string Kind, int NodeCount, object? Value)
{
    public static JsonValue Array(IReadOnlyList<JsonValue> items)
    {
        var count = 1;
        for (var i = 0; i < items.Count; i++) count += items[i].NodeCount;
        return new JsonValue("array", count, items);
    }

    public static JsonValue Object(IReadOnlyList<KeyValuePair<string, JsonValue>> members)
    {
        var count = 1;
        for (var i = 0; i < members.Count; i++) count += members[i].Value.NodeCount;
        return new JsonValue("object", count, members);
    }
}
