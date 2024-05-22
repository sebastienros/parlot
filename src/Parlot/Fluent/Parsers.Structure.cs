using System;
using System.Collections.Generic;
using System.Linq;

namespace Parlot.Fluent
{
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
            var choices = list.Select(l => new Then<TInput, Func<T, T, T>>(l.op, l.factory));

            return parser.And(ZeroOrMany(new OneOf<Func<T, T, T>>(choices.ToArray()).And(parser)))
                .Then(static x =>
                {
                    // multiplicative
                    var result = x.Item1;

                    // (("-" | "+") multiplicative ) *
                    foreach (var op in x.Item2)
                    {
                        result = op.Item1(result, op.Item2);
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
        public static Parser<T> RightAssociative<T, TInput>(this Parser<T> parser, params (Parser<TInput> op, Func<T, T, T> factory)[] list)
        {
            var choices = list.Select(l => new Then<TInput, Func<T, T, T>>(l.op, l.factory));

            return parser.And(ZeroOrMany(new OneOf<Func<T, T, T>>(choices.ToArray()).And(parser)))
                .Then(static x =>
                {
                    // a; (=, b); (^, c) -> = (a, ^(b, c))

                    var operations = x.Item2;

                    T result = default;

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
        /// Builds a parser that creates a unary operation.
        /// </summary>
        /// <typeparam name="T">The type of the returned parser.</typeparam>
        /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
        /// <param name="parser">The higher-priority parser the symbols are separating.</param>
        /// <param name="list">The list of operators that can be parsed and their associated result factory methods.</param>
        /// <returns></returns>
        public static Parser<T> Unary<T, TInput>(this Parser<T> parser, params (Parser<TInput> op, Func<T, T> factory)[] list)
        {
            return Recursive<T>((u) =>
            {
                var choices = list.Select(l => new Then<T, T>(l.op.SkipAnd(u), l.factory));
                return new OneOf<T>(choices.ToArray()).Or(parser);
            });
        }
    }
}
