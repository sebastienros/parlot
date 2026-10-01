using System;
using System.Linq;

namespace Parlot.Fluent;

public static partial class Parsers
{
    /// <summary>
    /// Builds a parser that creates a left-associative structure.
    /// c.f. https://en.wikipedia.org/wiki/Operator_associativity
    /// </summary>
    /// <typeparam name="T">The type of the returned parser.</typeparam>
    /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
    /// <param name="parser">The higher-priority parser the symbols are separating.</param>
    /// <param name="list">The list of operators that can be parsed and their associated result factory methods.</param>
    /// <returns></returns>
    /// <example>
    /// // additive => multiplicative(("-" | "+") multiplicative) * ;
    /// var additive = multiplicative.LeftAssociative(
    ///     (plus, static (a, b) => new Addition(a, b)),
    ///     (minus, static (a, b) => new Subtraction(a, b))
    ///     );
    /// </example>
    public static Parser<T> LeftAssociative<T, TInput>(this Parser<T> parser, params (Parser<TInput> op, Func<T, T, T> factory)[] list)
    {
        return new LeftAssociative<T, TInput>(parser, list);
    }

    /// <summary>
    /// Builds a parser that creates a left-associative structure.
    /// c.f. https://en.wikipedia.org/wiki/Operator_associativity
    /// </summary>
    /// <typeparam name="T">The type of the returned parser.</typeparam>
    /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
    /// <param name="parser">The higher-priority parser the symbols are separating.</param>
    /// <param name="list">The list of operators that can be parsed and their associated result factory methods.</param>
    /// <returns></returns>
    /// <example>
    /// // additive => multiplicative(("-" | "+") multiplicative) * ;
    /// var additive = multiplicative.LeftAssociative(
    ///     (plus, static (a, b) => new Addition(a, b)),
    ///     (minus, static (a, b) => new Subtraction(a, b))
    ///     );
    /// </example>
    public static Parser<T> LeftAssociative<T, TInput>(this Parser<T> parser, params (Parser<TInput> op, Func<ParseContext, T, T, T> factory)[] list)
    {
        return new LeftAssociativeWithContext<T, TInput>(parser, list);
    }

    /// <summary>Builds a left-associative parser using the values returned by its operator parsers.</summary>
    /// <typeparam name="T">The type of the operands and result.</typeparam>
    /// <typeparam name="TOperator">The type of the parsed operator value.</typeparam>
    /// <param name="parser">The higher-priority operand parser.</param>
    /// <param name="operators">The ordered operator parsers.</param>
    /// <param name="factory">Combines the left operand, right operand, and parsed operator.</param>
    /// <returns>A left-associative parser.</returns>
    public static Parser<T> LeftAssociative<T, TOperator>(this Parser<T> parser, Parser<TOperator>[] operators, Func<T, T, TOperator, T> factory)
    {
        return new AssociativeWithOperator<T, TOperator>(parser, operators, factory, rightAssociative: false);
    }

    /// <summary>Builds a left-associative parser using the parsed operator and the parse context.</summary>
    /// <typeparam name="T">The type of the operands and result.</typeparam>
    /// <typeparam name="TOperator">The type of the parsed operator value.</typeparam>
    /// <param name="parser">The higher-priority operand parser.</param>
    /// <param name="operators">The ordered operator parsers.</param>
    /// <param name="factory">Combines the context, left operand, right operand, and parsed operator.</param>
    /// <returns>A left-associative parser.</returns>
    public static Parser<T> LeftAssociative<T, TOperator>(this Parser<T> parser, Parser<TOperator>[] operators, Func<ParseContext, T, T, TOperator, T> factory)
    {
        return new AssociativeWithOperator<T, TOperator>(parser, operators, factory, rightAssociative: false);
    }

    /// <summary>
    /// Builds a parser that creates a right-associative structure.
    /// c.f. https://en.wikipedia.org/wiki/Operator_associativity
    /// </summary>
    /// <typeparam name="T">The type of the returned parser.</typeparam>
    /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
    /// <param name="parser">The higher-priority parser the symbols are separating.</param>
    /// <param name="list">The list of operators that can be parsed and their associated result factory methods.</param>
    /// <returns></returns>
    /// <example>
    /// // exponentiation => primary( ("^") primary) * ;
    /// var exponentiation = primary.RightAssociative(
    ///     (equal, static (a, b) => new Exponent(a, b))
    ///     );
    /// </example>
    public static Parser<T> RightAssociative<T, TInput>(this Parser<T> parser, params (Parser<TInput> op, Func<T, T, T> factory)[] list)
    {
        var choices = list.Select(l => new Then<TInput, Func<T, T, T>>(l.op, l.factory)).ToArray();

        return parser.And(ZeroOrMany(new OneOf<Func<T, T, T>>(choices).And(parser)))
            .Then(static x =>
            {
                // a; (=, b); (^, c) -> = (a, ^(b, c))

                var operations = x.Item2;

                T result;

                if (operations.Count > 0)
                {
                    result = operations[operations.Count - 1].Item2;

                    for (var i = operations.Count - 1; i > 0; i--)
                    {
                        result = operations[i].Item1(operations[i - 1].Item2, result);
                    }

                    result = operations[0].Item1(x.Item1, result);
                }
                else
                {
                    result = x.Item1;
                }

                return result;
            });
    }

    /// <summary>
    /// Builds a parser that creates a right-associative structure.
    /// c.f. https://en.wikipedia.org/wiki/Operator_associativity
    /// </summary>
    /// <typeparam name="T">The type of the returned parser.</typeparam>
    /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
    /// <param name="parser">The higher-priority parser the symbols are separating.</param>
    /// <param name="list">The list of operators that can be parsed and their associated result factory methods.</param>
    /// <returns></returns>
    /// <example>
    /// // exponentiation => primary( ("^") primary) * ;
    /// var exponentiation = primary.RightAssociative(
    ///     (equal, static (a, b) => new Exponent(a, b))
    ///     );
    /// </example>
    public static Parser<T> RightAssociative<T, TInput>(this Parser<T> parser, params (Parser<TInput> op, Func<ParseContext, T, T, T> factory)[] list)
    {
        var choices = list.Select(l => new Then<TInput, Func<ParseContext, T, T, T>>(l.op, l.factory)).ToArray();

        return parser.And(ZeroOrMany(new OneOf<Func<ParseContext, T, T, T>>(choices).And(parser)))
            .Then(static (context, x) =>
            {
                // a; (=, b); (^, c) -> = (a, ^(b, c))

                var operations = x.Item2;

                T result;

                if (operations.Count > 0)
                {
                    result = operations[operations.Count - 1].Item2;

                    for (var i = operations.Count - 1; i > 0; i--)
                    {
                        result = operations[i].Item1(context, operations[i - 1].Item2, result);
                    }

                    result = operations[0].Item1(context, x.Item1, result);
                }
                else
                {
                    result = x.Item1;
                }

                return result;
            });
    }

    /// <summary>Builds a right-associative parser using the values returned by its operator parsers.</summary>
    /// <typeparam name="T">The type of the operands and result.</typeparam>
    /// <typeparam name="TOperator">The type of the parsed operator value.</typeparam>
    /// <param name="parser">The higher-priority operand parser.</param>
    /// <param name="operators">The ordered operator parsers.</param>
    /// <param name="factory">Combines the left operand, right operand, and parsed operator.</param>
    /// <returns>A right-associative parser.</returns>
    public static Parser<T> RightAssociative<T, TOperator>(this Parser<T> parser, Parser<TOperator>[] operators, Func<T, T, TOperator, T> factory)
    {
        return new AssociativeWithOperator<T, TOperator>(parser, operators, factory, rightAssociative: true);
    }

    /// <summary>Builds a right-associative parser using the parsed operator and the parse context.</summary>
    /// <typeparam name="T">The type of the operands and result.</typeparam>
    /// <typeparam name="TOperator">The type of the parsed operator value.</typeparam>
    /// <param name="parser">The higher-priority operand parser.</param>
    /// <param name="operators">The ordered operator parsers.</param>
    /// <param name="factory">Combines the context, left operand, right operand, and parsed operator.</param>
    /// <returns>A right-associative parser.</returns>
    public static Parser<T> RightAssociative<T, TOperator>(this Parser<T> parser, Parser<TOperator>[] operators, Func<ParseContext, T, T, TOperator, T> factory)
    {
        return new AssociativeWithOperator<T, TOperator>(parser, operators, factory, rightAssociative: true);
    }

    /// <summary>
    /// Builds a parser that creates a unary operation.
    /// </summary>
    /// <typeparam name="T">The type of the returned parser.</typeparam>
    /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
    /// <param name="parser">The higher-priority parser the symbols are separating.</param>
    /// <param name="list">The list of operators that can be parsed and their associated result factory methods.</param>
    /// <returns></returns>
    public static Parser<T> Unary<T, TInput>(this Parser<T> parser, params (Parser<TInput> op, Func<T, T> factory)[] list)
    {
        return new Unary<T, TInput>(parser, list);
    }

    /// <summary>
    /// Builds a parser that creates a unary operation.
    /// </summary>
    /// <typeparam name="T">The type of the returned parser.</typeparam>
    /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
    /// <param name="parser">The higher-priority parser the symbols are separating.</param>
    /// <param name="list">The list of operators that can be parsed and their associated result factory methods.</param>
    /// <returns></returns>
    public static Parser<T> Unary<T, TInput>(this Parser<T> parser, params (Parser<TInput> op, Func<ParseContext, T, T> factory)[] list)
    {
        return new UnaryWithContext<T, TInput>(parser, list);
    }
}
