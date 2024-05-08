using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Calc;

using Domain;
using System.Numerics;

//TODO: FluentParser can be FluentParser<T>
public static class FluentParser 
{
    public static readonly Parser<Expression<decimal>> Expression;

    static FluentParser()
    {
        /*
         * Grammar:
         * expression     => factor ( ( "-" | "+" ) factor )* ;
         * factor         => unary ( ( "/" | "*" ) unary )* ;
         * unary          => ( "-" ) unary
         *                 | primary ;
         * primary        => NUMBER
         *                  | "(" expression ")" ;
         */

        // The Deferred helper creates a parser that can be referenced by others before it is defined
        var expression = Deferred<Expression<decimal>>();

            var number = Terms.Decimal()
                .Then<Expression<decimal>>(static d => new Number<decimal>(d))
                ;

        var divided = Terms.Char('/');
        var times = Terms.Char('*');
        var minus = Terms.Char('-');
        var plus = Terms.Char('+');
        var openParen = Terms.Char('(');
        var closeParen = Terms.Char(')');

        // "(" expression ")"
        var groupExpression = Between(openParen, expression, closeParen);

        // primary => NUMBER | "(" expression ")";
        var primary = number.Or<Expression<decimal>>(groupExpression);

        // The Recursive helper allows to create parsers that depend on themselves.
        // ( "-" ) unary | primary;
        var unary = Recursive<Expression<decimal>>((u) =>
            minus.And(u)
                .Then<Expression<decimal>>(static x => new NegateExpression<decimal>(x.Item2))
                .Or(primary));

        // factor => unary ( ( "/" | "*" ) unary )* ;
        var factor = unary.And(ZeroOrMany(divided.Or(times).And(unary)))
            .Then(static x =>
            {
                // unary
                var result = x.Item1;

                // (("/" | "*") unary ) *
                foreach (var op in x.Item2)
                {
                    result = op.Item1 switch
                    {
                        '/' => new Division<decimal>(result, op.Item2),
                        '*' => new Multiplication<decimal>(result, op.Item2),
                        _ => null
                    };
                }

                return result;
            });

        // expression => factor ( ( "-" | "+" ) factor )* ;
        expression.Parser = factor.And(ZeroOrMany(plus.Or(minus).And(factor)))
            .Then(static x =>
            {
                // factor
                var result = x.Item1;

                // (("-" | "+") factor ) *
                foreach (var op in x.Item2)
                {
                    result = op.Item1 switch
                    {
                        '+' => new Addition<decimal>(result, op.Item2),
                        '-' => new Subtraction<decimal>(result, op.Item2),
                        _ => null
                    };
                }

                return result;
            });            

        Expression = expression;
    }
}
