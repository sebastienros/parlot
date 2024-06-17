using System;
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
            var choices = list.Select(l => new Then<TInput, Func<ParseContext, T, T, T>>(l.op, l.factory));

            return parser.And(ZeroOrMany(new OneOf<Func<ParseContext, T, T, T>>(choices.ToArray()).And(parser)))
                .Then(static (context, x) =>
                {
                    // multiplicative
                    var result = x.Item1;

                    // (("-" | "+") multiplicative ) *
                    foreach (var op in x.Item2)
                    {
                        result = op.Item1(context, result, op.Item2);
                    }

                    return result;
                });
        }
        
        /// <summary>
        /// Builds a parser that creates a left-associative structure.
        /// c.f. https://en.wikipedia.org/wiki/Operator_associativity
        /// </summary>
        /// <typeparam name="T">The type of the returned parser.</typeparam>
        /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
        /// <param name="parser">The higher-priority parser the symbols are separating.</param>
        /// <param name="ops">The array of operators that can be parsed.</param>
        /// <param name="factory">The factory method to create the result of the operation.</param>
        /// <returns>A parser that creates a left-associative structure.</returns>
        public static Parser<T> LeftAssociative<T, TInput>(this Parser<T> parser, Parser<TInput>[] ops, Func<TInput, T, T, T> factory)
        {
            var choices = ops.Select(op => new Then<TInput, Func<T, T, T>>(op, operation => (a, b) => factory(operation, a, b)));

            return parser.And(ZeroOrMany(new OneOf<Func<T, T, T>>(choices.ToArray()).And(parser)))
                .Then(static x =>
                {
                    var result = x.Item1;

                    foreach (var op in x.Item2)
                    {
                        result = op.Item1(result, op.Item2);
                    }

                    return result;
                });
        }

        /// <summary>
        /// Builds a parser that creates a left-associative structure.
        /// c.f. https://en.wikipedia.org/wiki/Operator_associativity
        /// </summary>
        /// <typeparam name="T">The type of the returned parser.</typeparam>
        /// <typeparam name="TInput">The type of the symbol parsers.</typeparam>
        /// <param name="parser">The higher-priority parser the symbols are separating.</param>
        /// <param name="ops">The array of operators that can be parsed.</param>
        /// <param name="factory">The factory method to create the result of the operation.</param>
        /// <returns>A parser that creates a left-associative structure.</returns>
        public static Parser<T> LeftAssociative<T, TInput>(this Parser<T> parser, Parser<TInput>[] ops, Func<ParseContext, TInput, T, T, T> factory)
        {
            var choices = ops.Select(op => new Then<TInput, Func<ParseContext, T, T, T>>(op, operation => (context, a, b) => factory(context, operation, a, b)));

            return parser.And(ZeroOrMany(new OneOf<Func<ParseContext, T, T, T>>(choices.ToArray()).And(parser)))
                .Then(static (context, x) =>
                {
                    var result = x.Item1;

                    foreach (var op in x.Item2)
                    {
                        result = op.Item1(context, result, op.Item2);
                    }

                    return result;
                });
        }
    }
}
