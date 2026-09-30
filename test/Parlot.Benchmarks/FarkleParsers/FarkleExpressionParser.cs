using Farkle;
using Farkle.Builder;
using Farkle.Parser;
using Parlot.Tests.Calc;
using System;
using System.Globalization;

namespace Parlot.Benchmarks.FarkleParsers;

public static class FarkleExpressionParser
{
    public static readonly CharParser<Expression> Parser = CreateParser();

    private static CharParser<Expression> CreateParser()
    {
        var number = Terminal.Create<Expression>("Number",
            Regex.FromRegexString(@"([0-9]+(\.[0-9]*)?|\.[0-9]+)([eE][+-]?[0-9]+)?"),
            static (ref ParserState _, ReadOnlySpan<char> text) =>
                new Number(decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)));

        var expression = Nonterminal.Create<Expression>("Expression");
        var primary = Nonterminal.Create<Expression>("Primary",
            number.AsProduction(),
            "(".Appended().Extend(expression).Append(")").AsProduction());

        var unary = Nonterminal.Create<Expression>("Unary");
        unary.SetProductions(
            "-".Appended().Extend(unary).Finish<Expression>(static inner => new NegateExpression(inner)),
            primary.AsProduction());

        var multiplicative = Nonterminal.Create<Expression>("Multiplicative");
        multiplicative.SetProductions(
            multiplicative.Extended().Append("*").Extend(unary)
                .Finish<Expression>(static (left, right) => new Multiplication(left, right)),
            multiplicative.Extended().Append("/").Extend(unary)
                .Finish<Expression>(static (left, right) => new Division(left, right)),
            unary.AsProduction());

        expression.SetProductions(
            expression.Extended().Append("+").Extend(multiplicative)
                .Finish<Expression>(static (left, right) => new Addition(left, right)),
            expression.Extended().Append("-").Extend(multiplicative)
                .Finish<Expression>(static (left, right) => new Subtraction(left, right)),
            multiplicative.AsProduction());

        return expression.Build();
    }

    public static Expression Parse(string input) => Parser.Parse(input).Value;
}
