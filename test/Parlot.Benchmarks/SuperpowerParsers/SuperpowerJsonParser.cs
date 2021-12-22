using System.Collections.Generic;
using System.Linq;
using Parlot.Tests.Json;
using Superpower;

namespace Parlot.Benchmarks.SuperpowerParsers
{
    public static class SuperpowerJsonParser
    {
        private static TextParser<T> Between<T, U, V>(this Superpower.TextParser<T> p, Superpower.TextParser<U> before, Superpower.TextParser<V> after)
            => before.IgnoreThen(p).Then(x => after.Value(x));

        private static readonly TextParser<char> LBrace = Superpower.Parsers.Character.EqualTo('{');
        private static readonly TextParser<char> RBrace = Superpower.Parsers.Character.EqualTo('}');
        private static readonly TextParser<char> LBracket = Superpower.Parsers.Character.EqualTo('[');
        private static readonly TextParser<char> RBracket = Superpower.Parsers.Character.EqualTo(']');
        private static readonly TextParser<char> Quote = Superpower.Parsers.Character.EqualTo('"');
        private static readonly TextParser<char> Colon = Superpower.Parsers.Character.EqualTo(':');
        private static readonly TextParser<char> ColonWhitespace =
            Colon.Between(Superpower.Parsers.Character.WhiteSpace.Many(), Superpower.Parsers.Character.WhiteSpace.Many());
        private static readonly TextParser<char> Comma = Superpower.Parsers.Character.EqualTo(',');

        private static readonly TextParser<string> String =
            Superpower.Parsers.Character.Matching(c => c != '"', "char except quote")
                .Many()
                .Between(Quote, Quote)
                .Select(string.Concat);
        private static readonly TextParser<IJson> JsonString =
            String.Select(s => (IJson)new JsonString(s));

        private static readonly TextParser<IJson> Json =
            JsonString.Or(Superpower.Parse.Ref(() => JsonArray)).Or(Superpower.Parse.Ref(() => JsonObject));

        private static readonly TextParser<IJson> JsonArray =
            Json.Between(Superpower.Parsers.Character.WhiteSpace.Many(), Superpower.Parsers.Character.WhiteSpace.Many())
                .ManyDelimitedBy(Comma)
                .Between(LBracket, RBracket)
                .Select(els => (IJson)new JsonArray(els.ToArray()));

        private static readonly TextParser<KeyValuePair<string, IJson>> JsonMember =
            from name in String.SelectMany(_ => ColonWhitespace, (name, ws) => name)  // avoid allocating a transparent identifier for a result we don't care about
            from val in Json
            select new KeyValuePair<string, IJson>(name, val);

        private static readonly TextParser<IJson> JsonObject =
            JsonMember.Between(Superpower.Parsers.Character.WhiteSpace.Many(), Superpower.Parsers.Character.WhiteSpace.Many())
                .ManyDelimitedBy(Comma)
                .Between(LBrace, RBrace)
                .Select(kvps => (IJson)new JsonObject(new Dictionary<string, IJson>(kvps)));

        public static IJson Parse(string input) => Json.Parse(input);
    }
}
