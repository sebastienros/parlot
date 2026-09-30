using Farkle;
using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;
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
        // Farkle 7's built-in float terminals require digits on both sides of the decimal point.
        var digit = Regex.OneOf(('0', '9'));
        var digits = digit.AtLeast(1);
        var significand = Regex.Choice(
            digits + (Regex.Literal('.') + digit.ZeroOrMore()).Optional(),
            Regex.Literal('.') + digits);
        var exponent = Regex.Join(
            Regex.OneOf('e', 'E'),
            Regex.OneOf('+', '-').Optional(),
            digits).Optional();
        var number = Terminal.Create<Expression>("Number",
            (significand + exponent).CaseSensitive(),
            static (ref ParserState _, ReadOnlySpan<char> text) =>
                new Number(decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)));

        var expression = Nonterminal.Create<Expression>("Expression");
        expression.SetProductions(
            number.AsProduction(),
            expression.Extended().Append("+").Extend(expression)
                .Finish<Expression>(static (left, right) => new Addition(left, right)),
            expression.Extended().Append("-").Extend(expression)
                .Finish<Expression>(static (left, right) => new Subtraction(left, right)),
            expression.Extended().Append("*").Extend(expression)
                .Finish<Expression>(static (left, right) => new Multiplication(left, right)),
            expression.Extended().Append("/").Extend(expression)
                .Finish<Expression>(static (left, right) => new Division(left, right)),
            "-".Appended().Extend(expression).WithPrecedence(out var negation)
                .Finish<Expression>(static inner => new NegateExpression(inner)),
            "(".Appended().Extend(expression).Append(")").AsProduction());

        return expression.WithOperatorScope(new OperatorScope(
            new LeftAssociative("+", "-"),
            new LeftAssociative("*", "/"),
            new PrecedenceOnly(negation))).Build();
    }

    public static Expression Parse(string input) => Parser.Parse(input).Value;
}
