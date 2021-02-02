using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Calc
{
    public class FluentParser
    {
        public static readonly Parser<Expression> Expression;

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
            var groupExpression = Between(openParen, expression, closeParen);

            // primary => NUMBER | "(" expression ")";
            var primary = number.Or(groupExpression);

            // The Recursive helper allows to create parsers that depend on themselves.
            // ( "-" ) unary | primary;
            //var unary = Recursive<Expression>((u) => 
            //    minus.And(u)
            //        .Then<Expression>(static x => new NegateExpression(x.Item2))
            //        .Or(primary));

            // factor => unary ( ( "/" | "*" ) unary )* ;
            var factor = primary.And(ZeroOrMany(divided.Or(times).And(primary)))
                .Then(static x =>
                {
                    // unary
                    var result = x.Item1;

                    // (("/" | "*") unary ) *
                    foreach (var op in x.Item2)
                    {
                        result = op.Item1 switch
                        {
                            '/' => new Division(result, op.Item2),
                            '*' => new Multiplication(result, op.Item2),
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
                            '+' => new Addition(result, op.Item2),
                            '-' => new Subtraction(result, op.Item2),
                            _ => null
                        };
                    }

                    return result;
                });            

            Expression = expression;
        }
    }
}
