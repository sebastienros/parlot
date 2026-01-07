using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using Parlot.Tests.Calc;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// Generated parsers for benchmarking.
/// These parsers use the [GenerateParser] attribute to generate optimized parse methods at compile time.
/// </summary>
public static partial class GeneratedParsers
{
    /// <summary>
    /// A source-generated calculator expression parser.
    /// This is equivalent to FluentParser.Expression but uses compile-time generated code.
    /// </summary>
    [GenerateParser]
    public static Parser<Expression> ExpressionParser()
    {
        // Grammar:
        // additive       => multiplicative ( ( "-" | "+" ) multiplicative )* ;
        // multiplicative => unary ( ( "/" | "*" ) unary )* ;
        // unary          => ( "-" ) unary | primary ;
        // primary        => NUMBER | "(" expression ")" ;

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

    // Simple parser definitions for benchmarking individual combinators
    
    [GenerateParser]
    public static Parser<string> TextParser() => Terms.Text("hello");

    [GenerateParser]
    public static Parser<decimal> DecimalParser() => Terms.Decimal();

    [GenerateParser]
    public static Parser<string> OneOfParser() => OneOf(Terms.Text("apple"), Terms.Text("banana"), Terms.Text("cherry"));

    [GenerateParser]
    public static Parser<(string, decimal)> AndParser() => Terms.Text("price").And(Terms.Decimal());

    [GenerateParser]
    public static Parser<IReadOnlyList<decimal>> ZeroOrManyParser() => ZeroOrMany(Terms.Decimal());

    [GenerateParser]
    public static Parser<decimal> SkipWhiteSpaceParser() => SkipWhiteSpace(Literals.Decimal());
}
