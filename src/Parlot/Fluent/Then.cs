using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Returns a new <see cref="IParser{U}" /> converting the input value of 
    /// type T to the output value of type U using a custom function.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="U">The output parser type.</typeparam>
    public sealed class Then<T, U> : Parser<U>, IParser<U>
    {
        private readonly Func<T, U> _action1;
        private readonly Func<ParseContext, T, U> _action2;
        private readonly IParser<T> _parser;

        public Then(IParser<T> parser, Func<T, U> action)
        {
            _action1 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(IParser<T> parser, Func<ParseContext, T, U> action)
        {
            _action2 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
        {
            context.EnterParser(this);
            
            var parsed = new ParseResult<T>();

            if (_parser.Parse(context, ref parsed))
            {
                if (_action1 != null)
                {
                    result.Set(parsed.Start, parsed.End, _action1.Invoke(parsed.Value));
                }

                if (_action2 != null)
                {
                    result.Set(parsed.Start, parsed.End, _action2.Invoke(context, parsed.Value));
                }

                return true;
            }

            return false;
        }
    }
}
