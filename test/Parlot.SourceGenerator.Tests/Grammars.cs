using Parlot.Fluent;
using Parlot.Tests.Calc;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public static partial class Grammars
{
    [GenerateParser()]
    public static Parser<string> ParserWithNoName()
    {
        return Terms.Text("hello");
    }

    [GenerateParser("Hello")]
    public static Parser<string> HelloParser()
    {
        return Terms.Text("hello");
    }

    [GenerateParser("ParseExpression")]
    public static Parser<double> ExpressionParser()
    {
        var value = OneOf(
            Terms.Text("one").Then(_ => 1.0),
            Terms.Text("two").Then(_ => 2.0),
            Terms.Text("three").Then(_ => 3.0)
        );

        var tail = ZeroOrMany(Terms.Char('+').SkipAnd(value));

        return value.And(tail).Then(tuple =>
        {
            var (value, additions) = tuple;
            
            foreach (var v in additions)
            {
                value += v;
            }
            return value;
        });
    }

    [GenerateParser("ParseLeftAssociative")]
    public static Parser<double> LeftAssociativeParser()
    {
        var number = Terms.Decimal().Then(d => (double)d);
        var plus = Terms.Char('+');
        var minus = Terms.Char('-');

        return number.LeftAssociative(
            (plus, static (a, b) => a + b),
            (minus, static (a, b) => a - b)
        );
    }

    [GenerateParser("ParseNestedLeftAssociative")]
    public static Parser<double> NestedLeftAssociativeParser()
    {
        // Simulates multiplicative/additive precedence like a calculator
        var number = Terms.Decimal().Then(d => (double)d);
        
        var times = Terms.Char('*');
        var divided = Terms.Char('/');
        var plus = Terms.Char('+');
        var minus = Terms.Char('-');

        // multiplicative has higher precedence
        var multiplicative = number.LeftAssociative(
            (times, static (a, b) => a * b),
            (divided, static (a, b) => a / b)
        );

        // additive has lower precedence
        var additive = multiplicative.LeftAssociative(
            (plus, static (a, b) => a + b),
            (minus, static (a, b) => a - b)
        );

        return additive;
    }

    [GenerateParser("ParseCalculator")]
    public static Parser<Expression> CalculatorParser()
    {
        /*
         * Grammar:
         * additive       => multiplicative ( ( "-" | "+" ) multiplicative )* ;
         * multiplicative => unary ( ( "/" | "*" ) unary )* ;
         * unary          => ( "-" ) unary | primary ;
         * primary        => NUMBER | "(" expression ")" ;
        */

        // The Deferred helper creates a parser that can be referenced by others before it is defined
        var expression = Deferred<Expression>();

        var number = Terms.Decimal()
            .Then<Expression>(static d => new Parlot.Tests.Calc.Number(d));

        var divided = Terms.Char('/');
        var times = Terms.Char('*');
        var minus = Terms.Char('-');
        var plus = Terms.Char('+');
        var openParen = Terms.Char('(');
        var closeParen = Terms.Char(')');

        // "(" expression ")"
        var groupExpression = Between(openParen, expression, closeParen);

        // primary => NUMBER | "(" expression ")";
        var primary = number.Or(groupExpression);

        // ( "-" ) unary | primary;
        var unary = primary.Unary(
            (minus, static x => new Parlot.Tests.Calc.NegateExpression(x))
        );

        // multiplicative => unary ( ( "/" | "*" ) unary )* ;
        var multiplicative = unary.LeftAssociative(
            (divided, static (a, b) => new Parlot.Tests.Calc.Division(a, b)),
            (times, static (a, b) => new Parlot.Tests.Calc.Multiplication(a, b))
        );

        // additive => multiplicative(("-" | "+") multiplicative) * ;
        var additive = multiplicative.LeftAssociative(
            (plus, static (a, b) => new Parlot.Tests.Calc.Addition(a, b)),
            (minus, static (a, b) => new Parlot.Tests.Calc.Subtraction(a, b))
        );

        expression.Parser = additive;

        return expression;
    }
}