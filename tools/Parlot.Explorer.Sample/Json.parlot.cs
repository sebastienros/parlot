using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Explorer.Sample;

public static partial class Grammars
{
    [GenerateParser(nameof(Json), Diagnostics = true)]
    public static Parser<JsonValue> BuildJson()
    {
        var value = Deferred<JsonValue>();
        value.Named("value");
        var text = Terms.String(StringLiteralQuotes.Double).Named("string");
        var comma = Terms.Char(',').Named("comma");
        var member = text.AndSkip(Terms.Char(':').Named("colon")).And(value)
            .Then(static pair => new global::System.Collections.Generic.KeyValuePair<string, JsonValue>(pair.Item1.ToString(), pair.Item2)).Named("member");
        var array = Between(Terms.Char('[').Named("open array"), Separated(comma, value).Named("elements"), Terms.Char(']').Named("close array"))
            .Then(static items => JsonValue.Array(items)).Named("array");
        var obj = Between(Terms.Char('{').Named("open object"), Separated(comma, member).Named("members"), Terms.Char('}').Named("close object"))
            .Then(static members => JsonValue.Object(members)).Named("object");
        value.Parser = OneOf(
            text.Then(static text => new JsonValue("string", 1, text.ToString())).Named("string value"),
            Terms.Number<double>(NumberOptions.Float).Then(static number => new JsonValue("number", 1, number)).Named("number"),
            Terms.Text("true").Then(static _ => new JsonValue("boolean", 1, true)).Named("true"),
            Terms.Text("false").Then(static _ => new JsonValue("boolean", 1, false)).Named("false"),
            Terms.Text("null").Then(static _ => new JsonValue("null", 1, null)).Named("null"),
            array, obj).Named("JSON choice");
        return value.AndSkip(ZeroOrMany(Literals.WhiteSpace())).Eof().Named("JSON document");
    }
}
