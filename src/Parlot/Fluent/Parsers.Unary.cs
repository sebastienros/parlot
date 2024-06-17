using System;
using System.Linq;

namespace Parlot.Fluent
{
    public static partial class Parsers
    {
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
            return Recursive<T>(u =>
            {
                var choices = list.Select(l => new Then<T, T>(l.op.SkipAnd(u), l.factory));
                return new OneOf<T>(choices.ToArray()).Or(parser);
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
        public static Parser<T> Unary<T, TInput>(this Parser<T> parser, params (Parser<TInput> op, Func<ParseContext, T, T> factory)[] list)
        {
            return Recursive<T>(u =>
            {
                var choices = list.Select(l => new Then<T, T>(l.op.SkipAnd(u), l.factory));
                return new OneOf<T>(choices.ToArray()).Or(parser);
            });
        }
    }
}
