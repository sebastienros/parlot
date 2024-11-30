using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Calc;

public class FluentParser
{
    public static readonly Parser<Expression> Expression;

    static FluentParser()
    {
        /*
         * Grammar:
         * The top declaration has a lower priority than the lower one.
         * 
         * additive       => multiplicative ( ( "-" | "+" ) multiplicative )* ;
         * multiplicative => unary ( ( "/" | "*" ) unary )* ;
         * unary          => ( "-" ) unary
         *                   | primary ;
         * primary        => NUMBER
         *                   | "(" expression ")" ;
        */

        // The Deferred helper creates a parser that can be referenced by others before it is defined
        var expression = Deferred<Expression>();

        var number = Terms.Decimal()
            .Then<Expression>(static d => new Number(d))
            ;

        var divided = Terms.Char('/');
        var times = Terms.Char('*');
        var minus = Terms.Char('-');
        var plus = Terms.Char('+');
        var openParen = Terms.Char('(');
        var closeParen = Terms.Char(')');

        // "(" expression ")"
        var groupExpression = Between(openParen, expression, closeParen).Named("group");

        // primary => NUMBER | "(" expression ")";
        var primary = number.Or(groupExpression).Named("primary");

        // ( "-" ) unary | primary;
        var unary = primary.Unary(
            (minus, x => new NegateExpression(x))
            ).Named("unary");

        // multiplicative => unary ( ( "/" | "*" ) unary )* ;
        var multiplicative = unary.LeftAssociative(
            (divided, static (a, b) => new Division(a, b)),
            (times, static (a, b) => new Multiplication(a, b))
            ).Named("multiplicative");

        // additive => multiplicative(("-" | "+") multiplicative) * ;
        var additive = multiplicative.LeftAssociative(
            (plus, static (a, b) => new Addition(a, b)),
            (minus, static (a, b) => new Subtraction(a, b))
            ).Named("additive");

        expression.Parser = additive;

        expression.Named("expression");

        Expression = expression;
    }
}
