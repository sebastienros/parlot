using System;
using System.Collections.Generic;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Json;

/// <summary>Demonstrates retaining JSON token spans instead of materializing strings.</summary>
public static class JsonRetainedTextParser
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

        public AllocationContext(string input, bool optimize) : base(new Scanner(input))
        {
            _optimize = optimize;
        }

        public IJson CreateString(TextSpan token) => _optimize
            ? new JsonRetainedString(token)
            : new JsonString(token.ToString());

        public IJson CreateObject(IReadOnlyList<(TextSpan, IJson)> members)
        {
            if (_optimize)
            {
                var values = new Dictionary<TextSpan, IJson>(members.Count);
                foreach (var (key, value) in members)
                {
                    values[key] = value;
                }
                return new JsonRetainedObject(values);
            }
            else
            {
                var values = new Dictionary<string, IJson>(members.Count, StringComparer.Ordinal);
                foreach (var (key, value) in members)
                {
                    values[key.ToString()] = value;
                }
                return new JsonObject(values);
            }
        }
    }
}

/// <summary>A string token that retains its source buffer.</summary>
public sealed class JsonRetainedString : IJson
{
    public TextSpan Value { get; }
    public JsonRetainedString(TextSpan value) => Value = value;
}

/// <summary>Object keys retain their source buffers.</summary>
public sealed class JsonRetainedObject : IJson
{
    public IDictionary<TextSpan, IJson> Members { get; }
    public JsonRetainedObject(IDictionary<TextSpan, IJson> members) => Members = members;
}
