using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Calc
{
    public class FluentParser
    {
        public static readonly IParser<Expression> Expression;

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

            // Make deferred Lazy since it's a cyclic reference
            var expression = Deferred<Expression>();

            var number = Literals.Decimal()
                .Then(static d => (Expression) new Number(d))
                ;

            var divided = Literals.Char('/');
            var times = Literals.Char('*');
            var minus = Literals.Char('-');
            var plus = Literals.Char('+');

            // "(" expression ")"
            var groupExpression = Between("(", expression, ")");

            // primary => NUMBER | "(" expression ")";
            var primary = number.Or(groupExpression);

            // Make unary deferred since it's a cyclic reference
            var unary = Deferred<Expression>();

            // ( "-" ) unary | primary;
            unary.Parser = minus.And(unary)
                .Then(static x => (Expression) new NegateExpression(x.Item2))
                .Or(primary);

            // factor => unary ( ( "/" | "*" ) unary )* ;
            var factor = unary.And(Star(divided.Or(times).And(unary)))
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
            expression.Parser = factor.And(Star(plus.Or(minus).And(factor)))
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
                            '-' => new Substraction(result, op.Item2),
                            _ => null
                        };
                    }

                    return result;
                });            

            Expression = expression;
        }
    }
}
