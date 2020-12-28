using Parlot.Fluent;
using System.Collections.Generic;
using System.Collections.Immutable;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Json
{
    public class JsonParser
    {
        private static readonly IParser<IJson> Json;

        static JsonParser()
        {
            var LBrace = Literals.Char('{');
            var RBrace = Literals.Char('}');
            var LBracket = Literals.Char('[');
            var RBracket = Literals.Char(']');
            var Colon = Literals.Char(':');
            var Comma = Literals.Char(',');

            var String = Terms.String(StringLiteralQuotes.Double);

            var jsonString =
                String.Then<IJson>(static s => new JsonString(Character.DecodeString(s.Text).ToString()));

            var json = Deferred<IJson>();

            var jsonArray =
                Between(LBracket, Separated(Comma, json), RBracket)
                    .Then<IJson>(static els => new JsonArray(els.ToImmutableArray()));

            var jsonMember =
                String.And(Colon).And(json)
                .Then(static x => new KeyValuePair<string, IJson>(x.Item1.Text[1..^1], x.Item3));

            var jsonObject =
                Between(LBrace, Separated(Comma, jsonMember), RBrace)
                    .Then<IJson>(static kvps => new JsonObject(kvps.ToImmutableDictionary()));

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
