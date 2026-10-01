using Farkle;
using Farkle.Builder;
using Parlot.Tests.Json;
using System;
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

        var elements = value.SeparatedBy<IJson, List<IJson>>(comma, atLeastOnce: true);
        var members = Nonterminal.Create<Dictionary<string, IJson>>("Members");
        members.SetProductions(
            text.Extended().Append(":").Extend(value)
                .Finish(static (name, item) => new Dictionary<string, IJson> { { name, item } }),
            members.Extended().Append(comma).Extend(text).Append(":").Extend(value)
                .Finish(static (items, name, item) =>
                {
                    items.Add(name, item);
                    return items;
                }));

        value.SetProductions(
            text.Extended().Finish<IJson>(static text => new JsonString(text)),
            "[".Appended().Append("]")
                .Finish<IJson>(static () => new JsonArray(Array.Empty<IJson>())),
            "[".Appended().Extend(elements).Append("]")
                .Finish<IJson>(static items => new JsonArray(items)),
            "{".Appended().Append("}")
                .Finish<IJson>(static () => new JsonObject(new Dictionary<string, IJson>())),
            "{".Appended().Extend(members).Append("}")
                .Finish<IJson>(static items => new JsonObject(items)));

        return value.Build();
    }

    public static IJson Parse(string input) => Parser.Parse(input).Value;
}
