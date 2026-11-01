using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using Parlot.Tests.Json;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class FailurePositionBenchmarks
{
    // The JSON grammar of GeneratedParsers, which must match the whole input
    [GenerateParser(nameof(TryParse))]
    [IncludeUsings("System.Collections.Generic", "Parlot.Tests.Json")]
    private static Parser<IJson> BuildJson()
    {
        var lBrace = Terms.Char('{');
        var rBrace = Terms.Char('}');
        var lBracket = Terms.Char('[');
        var rBracket = Terms.Char(']');
        var colon = Terms.Char(':');
        var comma = Terms.Char(',');
        var json = Deferred<IJson>();

        var jsonString = Terms.String(StringLiteralQuotes.Double)
            .Then<IJson>(static value => new JsonString(value.ToString()));

        var jsonArray = Between(lBracket, Separated(comma, json), rBracket)
            .Then<IJson>(static elements => new JsonArray(elements));

        var jsonMember = Terms.String(StringLiteralQuotes.Double).And(colon).And(json)
            .Then(static member => new KeyValuePair<string, IJson>(member.Item1.ToString(), member.Item3));

        var jsonObject = Between(lBrace, Separated(comma, jsonMember), rBrace)
            .Then<IJson>(static members => new JsonObject(new Dictionary<string, IJson>(members)));

        json.Parser = OneOf(jsonString, jsonArray, jsonObject);

        return json.Eof();
    }
}
