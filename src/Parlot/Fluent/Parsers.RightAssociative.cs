using System;
using System.Linq;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
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
        public static Parser<T> RightAssociative<T, TInput>(this Parser<T> parser,
            params (Parser<TInput> op, Func<T, T, T> factory)[] list)
        {
            var choices = list.Select(l => new Then<TInput, Func<T, T, T>>(l.op, l.factory));

            return parser.And(ZeroOrMany(new OneOf<Func<T, T, T>>(choices.ToArray()).And(parser)))
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
        public static Parser<T> RightAssociative<T, TInput>(this Parser<T> parser,
            params (Parser<TInput> op, Func<ParseContext, T, T, T> factory)[] list)
        {
            var choices = list.Select(l => new Then<TInput, Func<ParseContext, T, T, T>>(l.op, l.factory));

            return parser.And(ZeroOrMany(new OneOf<Func<ParseContext, T, T, T>>(choices.ToArray()).And(parser)))
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

        /// <summary>
        /// Builds a parser that creates a right-associative structure.
        /// c.f. https://en.wikipedia.org/wiki/Operator_associativity
        /// </summary>
        /// <typeparam name="T">The type of the returned parser.</typeparam>
        /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
        /// <param name="parser">The higher-priority parser the symbols are separating.</param>
        /// <param name="ops">The list of operator parsers.</param>
        /// <param name="factory">The factory method for creating results from operators and operands.</param>
        /// <returns>The parser for a right-associative structure.</returns>
        public static Parser<T> RightAssociative<T, TInput>(this Parser<T> parser, Parser<TInput>[] ops,
            Func<TInput, T, T, T> factory)
        {
            var choices = ops.Select(op =>
                new Then<TInput, Func<T, T, T>>(op, operation => (a, b) => factory(operation, a, b)));

            return parser.And(ZeroOrMany(new OneOf<Func<T, T, T>>(choices.ToArray()).And(parser)))
                .Then(static x =>
                {
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
        /// <param name="ops">The list of operator parsers.</param>
        /// <param name="factory">The factory method for creating results from operators and operands.</param>
        /// <returns>The parser for a right-associative structure.</returns>
        public static Parser<T> RightAssociative<T, TInput>(this Parser<T> parser, Parser<TInput>[] ops,
            Func<ParseContext, TInput, T, T, T> factory)
        {
            var choices = ops.Select(op =>
                new Then<TInput, Func<ParseContext, T, T, T>>(op,
                    operation => (context, a, b) => factory(context, operation, a, b)));

            return parser.And(ZeroOrMany(new OneOf<Func<ParseContext, T, T, T>>(choices.ToArray()).And(parser)))
                .Then(static (context, x) =>
                {
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
    }
}
