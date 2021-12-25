using System.Collections.Generic;
using System.Linq;

namespace Parlot.Tests.Json
{
    public interface IJson
    {
    }

    public class JsonArray : IJson
    {
        public IJson[] Elements { get; }
        public JsonArray(IJson[] elements)
        {
            Elements = elements;
        }
        public override string ToString()
            => $"[{string.Join(",", Elements.Select(e => e.ToString()))}]";
    }

    public class JsonObject : IJson
    {
        public IDictionary<string, IJson> Members { get; }
        public JsonObject(IDictionary<string, IJson> members)
        {
            Members = members;
        }
        public override string ToString()
            => $"{{{string.Join(",", Members.Select(kvp => $"\"{kvp.Key}\":{kvp.Value}"))}}}";
    }

    public class JsonString : IJson
    {
        public string Value { get; }
        public JsonString(string value)
        {
            Value = value;
        }

        public override string ToString()
            => $"\"{Value}\"";
    }
}
