using System;
using System.Collections.Generic;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Json;

/// <summary>Demonstrates a bounded document-scoped pool for repeated short JSON strings.</summary>
public static class JsonDocumentPoolParser
{
    private static readonly Parser<IJson> _json = Build();

    public static IJson Parse(string input, bool optimize = true)
    {
        var context = new AllocationContext(input, optimize);
        return _json.TryParse(context, out var result, out _) ? result : null;
    }

    private static Parser<IJson> Build()
    {
        var json = Deferred<IJson>();
        var text = Terms.String(StringLiteralQuotes.Double);
        var value = text.Then<IJson>(static (context, token) => ((AllocationContext)context).CreateString(token));
        var array = Between(Terms.Char('['), Separated(Terms.Char(','), json), Terms.Char(']'))
            .Then<IJson>(static elements => new JsonArray(elements));
        var member = text.AndSkip(Terms.Char(':')).And(json);
        var obj = Between(Terms.Char('{'), Separated(Terms.Char(','), member), Terms.Char('}'))
            .Then<IJson>(static (context, members) => ((AllocationContext)context).CreateObject(members));
        json.Parser = OneOf(value, array, obj);
        return json.Eof();
    }

    private sealed class AllocationContext : ParseContext
    {
        private readonly bool _optimize;
        private Dictionary<TextSpan, string> _strings;

        public AllocationContext(string input, bool optimize) : base(new Scanner(input))
        {
            _optimize = optimize;
        }

        public JsonString CreateString(TextSpan token) => new JsonString(Materialize(token));

        public JsonObject CreateObject(IReadOnlyList<(TextSpan, IJson)> members)
        {
            var values = new Dictionary<string, IJson>(members.Count, StringComparer.Ordinal);
            foreach (var (key, value) in members)
            {
                values[Materialize(key)] = value;
            }
            return new JsonObject(values);
        }

        private string Materialize(TextSpan token)
        {
            if (!_optimize || token.Length > 32)
            {
                return token.ToString();
            }

            _strings ??= new Dictionary<TextSpan, string>();
            if (_strings.TryGetValue(token, out var value))
            {
                return value;
            }
            value = token.ToString();
            if (_strings.Count < 256)
            {
                _strings.Add(token, value);
            }
            return value;
        }
    }
}
