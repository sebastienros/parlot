using Parlot.Tests.Json;
using Pidgin;
using System.Collections.Generic;
using System.Linq;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace Parlot.Benchmarks.PidginParsers
{
    public static class PidginJsonParser
    {
        private static readonly Parser<char, char> LBrace = Char('{');
        private static readonly Parser<char, char> RBrace = Char('}');
        private static readonly Parser<char, char> LBracket = Char('[');
        private static readonly Parser<char, char> RBracket = Char(']');
        private static readonly Parser<char, char> Quote = Char('"');
        private static readonly Parser<char, char> Colon = Char(':');
        private static readonly Parser<char, char> ColonWhitespace =
            Colon.Between(SkipWhitespaces);
        private static readonly Parser<char, char> Comma = Char(',');

        private static readonly Parser<char, string> String =
            Token(c => c != '"')
                .ManyString()
                .Between(Quote);
        private static readonly Parser<char, IJson> JsonString =
            String.Select<IJson>(s => new JsonString(s));

        private static readonly Parser<char, IJson> Json =
            JsonString.Or(Rec(() => JsonArray)).Or(Rec(() => JsonObject));

        private static readonly Parser<char, IJson> JsonArray =
            Json.Between(SkipWhitespaces)
                .Separated(Comma)
                .Between(LBracket, RBracket)
                .Select<IJson>(els => new JsonArray(els.ToArray()));

        private static readonly Parser<char, KeyValuePair<string, IJson>> JsonMember =
            String
                .Before(ColonWhitespace)
                .Then(Json, (name, val) => new KeyValuePair<string, IJson>(name, val));

        private static readonly Parser<char, IJson> JsonObject =
            JsonMember.Between(SkipWhitespaces)
                .Separated(Comma)
                .Between(LBrace, RBrace)
                .Select<IJson>(kvps => new JsonObject(new Dictionary<string, IJson>(kvps)));

        public static Result<char, IJson> Parse(string input) => Json.Parse(input);
    }
}
