using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Returns a new <see cref="Parser{U}" /> converting the input value of 
    /// type T to the output value of type U using a custom function.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    public sealed class ScopedParser<T> : Parser<T>
    {
        private readonly Parser<T> _parser;

        public ScopedParser(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            return _parser.Parse(new ParseContext(context), ref result);
        }
    }
}
