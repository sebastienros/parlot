using Parlot.Fluent;
using System;

namespace Parlot.Compilation
{
    /// <summary>
    /// An instance of this class encapsulates the result of a compiled parser
    /// in order to expose is as as standard parser contract.
    /// </summary>
    /// <remarks>
    /// This class is used in <see cref="Parser{T}.Compile"/>.
    /// </remarks>
    public class CompiledParser<T> : Parser<T>
    {
        private readonly Func<ParseContext, T> _parse;

        public CompiledParser(Func<ParseContext, T> parse)
        {
            _parse = parse ?? throw new ArgumentNullException(nameof(parse));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            var start = context.Scanner.Cursor.Offset;
            var parsed = _parse(context);

            result.Set(start, context.Scanner.Cursor.Offset, parsed);
            return true;
        }
    }
}
