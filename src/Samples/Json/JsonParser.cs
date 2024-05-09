using Parlot.Fluent;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Json
{
    public class JsonParser
    {
        public static readonly Parser<IJson> Json;

        static JsonParser()
        {
            var LBrace = Terms.Char('{');
            var RBrace = Terms.Char('}');
            var LBracket = Terms.Char('[');
            var RBracket = Terms.Char(']');
            var Colon = Terms.Char(':');
            var Comma = Terms.Char(',');

            var String = Terms.String(StringLiteralQuotes.Double);

            var jsonString =
                String
                    .Then<IJson>(static s => new JsonString(s.ToString()));

            var json = Deferred<IJson>();

            var jsonArray =
                Between(LBracket, Separated(Comma, json), RBracket)
                    .Then<IJson>(static els => new JsonArray(els.ToArray()));

            var jsonMember =
                String.And(Colon).And(json)
                    .Then(static member => new KeyValuePair<string, IJson>(member.Item1.ToString(), member.Item3));

            var jsonObject =
                Between(LBrace, Separated(Comma, jsonMember), RBrace)
                    .Then<IJson>(static kvps => new JsonObject(new Dictionary<string, IJson>(kvps)));

            Json = json.Parser = jsonString.Or(jsonArray).Or(jsonObject);
        }

        public static IJson Parse(string input)
        {
            if (Json.TryParse(input, out var result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }
    }
}
