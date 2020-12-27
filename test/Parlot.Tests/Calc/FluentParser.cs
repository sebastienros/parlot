using Parlot.Fluent;
using static Parlot.Fluent.ParserBuilder;

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

            // Make expression Lazy since it's a cyclic reference
            var expression = Lazy<Expression>();

            var number = Literals
                .Decimal()
                .Then(static d => (Expression) new Number(d))
                ;

            // "(" expression ")"
            var groupExpression = Sequence(
               Literals.Char('('),
               expression,
               Literals.Char(')')
               ).Then(static x => x.Item2);

            // primary => NUMBER | "(" expression ")";
            var primary = OneOf(number, groupExpression);

            // Make unary Lazy since it's a cyclic reference
            var unary = Lazy<Expression>();

            // ( "-" ) unary | primary;
            unary.Parser = OneOf(
                Sequence(
                    Literals.Char('-'),
                    unary
                    ).Then(static x => (Expression)new NegateExpression(x.Item2)),
                primary
            );

            // factor         => unary ( ( "/" | "*" ) unary )* ;
            var factor = Sequence(
                unary,
                ZeroOrMany(
                    Sequence(
                        OneOf(
                            Literals.Char('/'),
                            Literals.Char('*')
                        ),
                        unary
                    ))
                ).Then(static x =>
                {
                    // unary
                    Expression result = x.Item1;

                    // (("/" | "*") unary ) *
                    foreach (var op in x.Item2)
                    {
                        switch (op.Item1)
                        {
                            case '/': result = new Division(result, op.Item2); break;
                            case '*': result = new Multiplication(result, op.Item2); break;
                        }
                    }

                    return result;
                });

            // expression     => factor ( ( "-" | "+" ) factor )* ;
            expression.Parser = Sequence(
                factor,
                ZeroOrMany(
                    Sequence(
                        OneOf(
                            Literals.Char('+'),
                            Literals.Char('-')
                        ),
                        factor
                    )
                )
                ).Then(static x =>
                {
                    // factor
                    Expression result = x.Item1;

                    // (("-" | "+") factor ) *
                    foreach (var op in x.Item2)
                    {
                        switch (op.Item1)
                        {
                            case '+': result = new Addition(result, op.Item2); break;
                            case '-': result = new Substraction(result, op.Item2); break;
                        }
                    }

                    return result;
                });           
            

            Expression = expression;
        }
    }
}
