using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public static partial class GeneratedParsers
{
    [GenerateParser(nameof(TryParseExpression))]
    [IncludeUsings("Parlot.Tests.Calc")]
    private static Parser<Expression> BuildExpression()
    {
        var expression = Deferred<Expression>();

        var number = Terms.Decimal()
            .Then<Expression>(static value => new Number(value));

        var divided = Terms.Char('/');
        var times = Terms.Char('*');
        var minus = Terms.Char('-');
        var plus = Terms.Char('+');
        var openParen = Terms.Char('(');
        var closeParen = Terms.Char(')');

        var groupExpression = Between(openParen, expression, closeParen);
        var primary = number.Or(groupExpression);

        var unary = primary.Unary(
            (minus, static value => new NegateExpression(value))
        );

        var multiplicative = unary.LeftAssociative(
            (divided, static (left, right) => new Division(left, right)),
            (times, static (left, right) => new Multiplication(left, right))
        );

        var additive = multiplicative.LeftAssociative(
            (plus, static (left, right) => new Addition(left, right)),
            (minus, static (left, right) => new Subtraction(left, right))
        );

        expression.Parser = additive;

        return expression;
    }

    [GenerateParser(nameof(TryParseJson))]
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

        return json;
    }

    [GenerateParser(nameof(TryParseText))]
    private static Parser<string> BuildText() => Terms.Text("hello");

    [GenerateParser(nameof(TryParseDecimal))]
    private static Parser<decimal> BuildDecimal() => Terms.Decimal();

    [GenerateParser(nameof(TryParseInteger))]
    private static Parser<long> BuildInteger() => Terms.Integer();

    [GenerateParser(nameof(TryParseOneOf))]
    private static Parser<string> BuildOneOf() =>
        OneOf(Terms.Text("apple"), Terms.Text("banana"), Terms.Text("cherry"));

    [GenerateParser(nameof(TryParseLiteralOneOf))]
    private static Parser<string> BuildLiteralOneOf() =>
        OneOf(Literals.Text("apple"), Literals.Text("banana"), Literals.Text("cherry"));

    [GenerateParser(nameof(TryParseAnd))]
    private static Parser<(string, decimal)> BuildAnd() =>
        Terms.Text("price").And(Terms.Decimal());

    [GenerateParser(nameof(TryParseZeroOrMany))]
    private static Parser<IReadOnlyList<decimal>> BuildZeroOrMany() =>
        ZeroOrMany(Terms.Decimal());

    [GenerateParser(nameof(TryParseSkipWhiteSpace))]
    private static Parser<decimal> BuildSkipWhiteSpace() =>
        SkipWhiteSpace(Literals.Decimal());
}
