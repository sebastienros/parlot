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
    /// This class is used in <see cref="Parser{T}.Compile"/>.
    /// </remarks>
    public class CompiledParser<T> : Parser<T>, ICompiledParser
    {
        private readonly Func<ParseContext, ValueTuple<bool, T>> _parse;

        public CompiledParser(Func<ParseContext, ValueTuple<bool, T>> parse)
        {
            _parse = parse ?? throw new ArgumentNullException(nameof(parse));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
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
