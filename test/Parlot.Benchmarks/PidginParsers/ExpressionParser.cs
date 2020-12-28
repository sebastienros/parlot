using Parlot.Tests.Calc;
using Pidgin;
using Pidgin.Expression;
using System;
using static Pidgin.Parser;

namespace Parlot.Benchmarks.PidginParsers
{
    public static class ExprParser
    {
        private static Parser<char, T> Tok<T>(Parser<char, T> token)
            => Try(token).Before(SkipWhitespaces);
        private static Parser<char, string> Tok(string token)
            => Tok(String(token));

        private static Parser<char, T> Parenthesised<T>(Parser<char, T> parser)
            => parser.Between(Tok("("), Tok(")"));

        private static Parser<char, Func<Expression, Expression, Expression>> Binary(Parser<char, string> op)
            => op.Select<Func<Expression, Expression, Expression>>(type => (l, r) => 
            {
                return type switch
                {
                    "+" => new Addition(l, r),
                    "-" => new Substraction(l, r),
                    "*" => new Multiplication(l, r),
                    "/" => new Division(l, r),
                    _ => null,
                };
            }
        );
        
        private static Parser<char, Func<Expression, Expression>> Unary(Parser<char, string> op)
            => op.Select<Func<Expression, Expression>>(type => o => new NegateExpression(o));

        private static readonly Parser<char, Func<Expression, Expression, Expression>> Add
            = Binary(Tok("+").ThenReturn("+"));
        private static readonly Parser<char, Func<Expression, Expression, Expression>> Sub
            = Binary(Tok("-").ThenReturn("-"));
        private static readonly Parser<char, Func<Expression, Expression, Expression>> Mul
            = Binary(Tok("*").ThenReturn("*"));
        private static readonly Parser<char, Func<Expression, Expression, Expression>> Div
            = Binary(Tok("/").ThenReturn("/"));
        private static readonly Parser<char, Func<Expression, Expression>> Neg
            = Unary(Tok("-").ThenReturn("-"));

        private static readonly Parser<char, Expression> Literal
            = Tok(Real)
                .Select<Expression>(value => new Number((decimal) value))
                .Labelled("decimal literal");

        private static readonly Parser<char, Expression> Expr = ExpressionParser.Build<char, Expression>(
            expr => (
                OneOf(
                    Literal,
                    Parenthesised(expr).Labelled("parenthesised expression")
                ),
                new[]
                {
                    Operator.Prefix(Neg),
                    Operator.InfixL(Mul).And(Operator.InfixL(Div)),
                    Operator.InfixL(Add).And(Operator.InfixL(Sub))
                }
            )
        ).Labelled("expression");

        public static Expression ParseOrThrow(string input)
            => Expr.ParseOrThrow(input);
    }
}
