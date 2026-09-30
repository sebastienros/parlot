using Farkle;
using Farkle.Builder;
using Parlot.Tests.Json;
using System.Collections.Generic;

namespace Parlot.Benchmarks.FarkleParsers;

public static class FarkleJsonParser
{
    public static readonly CharParser<IJson> Parser = CreateParser();

    private static CharParser<IJson> CreateParser()
    {
        var text = Terminals.String("String", '"');
        var value = Nonterminal.Create<IJson>("Value");
        var comma = Terminal.Literal(",");

        var elements = value.SeparatedBy<IJson, List<IJson>>(comma);
        var array = Nonterminal.Create<IJson>("Array",
            "[".Appended().Extend(elements).Append("]")
                .Finish<IJson>(static items => new JsonArray(items.ToArray())));

        var member = Nonterminal.Create("Member",
            text.Extended().Append(":").Extend(value)
                .Finish(static (name, item) => new KeyValuePair<string, IJson>(name, item)));
        var members = member.SeparatedBy<KeyValuePair<string, IJson>, List<KeyValuePair<string, IJson>>>(comma);
        var jsonObject = Nonterminal.Create<IJson>("Object",
            "{".Appended().Extend(members).Append("}")
                .Finish<IJson>(static items => new JsonObject(new Dictionary<string, IJson>(items))));

        value.SetProductions(
            text.Extended().Finish<IJson>(static text => new JsonString(text)),
            array.AsProduction(),
            jsonObject.AsProduction());

        return value.Build();
    }

    public static IJson Parse(string input) => Parser.Parse(input).Value;
}
