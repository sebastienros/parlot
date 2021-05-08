using Parlot.Fluent;
using System;

namespace Parlot.Compilation
{
    /// <summary>
    /// Marker interface to detect a Parser has already been compiled.
    /// </summary>
    public interface ICompiledParser
    {

    }

    /// <summary>
    /// An instance of this class encapsulates the result of a compiled parser
    /// in order to expose is as as standard parser contract.
    /// </summary>
    /// <remarks>
    /// This class is used in <see cref="Parsers.Compile{T, TParseContext,TChar}"/>.
    /// </remarks>
    public class CompiledParser<T, TParseContext, TChar> : Parser<T, TParseContext, TChar>, ICompiledParser
    where TParseContext : ParseContextWithScanner<Scanner<TChar>, TChar>
    where TChar : IEquatable<TChar>, IConvertible
    {
        private readonly Func<TParseContext, ValueTuple<bool, T>> _parse;

        public CompiledParser(Func<TParseContext, ValueTuple<bool, T>> parse)
        {
            _parse = parse ?? throw new ArgumentNullException(nameof(parse));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            var start = context.Scanner.Cursor.Offset;
            var parsed = _parse(context);

            if (parsed.Item1)
            {
                result.Set(start, context.Scanner.Cursor.Offset, parsed.Item2);
                return true;
            }

            return false;
        }
    }
}
