using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Returns a new <see cref="Parser{T,TParseContext}" /> converting the input parser of 
    /// type T to a scoped parsed.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="TParseContext">The parser context type.</typeparam>
    public sealed class ScopedParser<T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext<TParseContext>
    {
        private readonly IParser<T, TParseContext> _parser;

        public ScopedParser(IParser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            return _parser.Parse(context.Scope(), ref result);
        }
    }
}
