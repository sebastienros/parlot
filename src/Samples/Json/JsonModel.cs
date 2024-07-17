using System.Collections.Generic;
using System.Linq;

namespace Parlot.Tests.Json
{
    public interface IJson
    {
    }

    public class JsonArray(IReadOnlyList<IJson> elements) : IJson
    {
        public IReadOnlyList<IJson> Elements { get; } = elements;

        public override string ToString()
            => $"[{string.Join(",", Elements.Select(e => e.ToString()))}]";
    }

    public class JsonObject(IDictionary<string, IJson> members) : IJson
    {
        public IDictionary<string, IJson> Members { get; } = members;

        public override string ToString()
            => $"{{{string.Join(",", Members.Select(kvp => $"\"{kvp.Key}\":{kvp.Value}"))}}}";
    }

    public class JsonString(string value) : IJson
    {
        public string Value { get; } = value;

        public override string ToString()
            => $"\"{Value}\"";
    }
}
