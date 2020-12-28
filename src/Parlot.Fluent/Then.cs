using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Returns a new <see cref="IParser{TResult}" /> converting the input value of 
    /// type <see cref="T"/> to the output value of type <see cref="U"/> using a custom function.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="U">The output parser type.</typeparam>
    public sealed class Then<T, U> : Parser<U>, IParser<U>
    {
        private readonly Func<T, U> _action;
        private readonly IParser<T> _parser;

        public Then(IParser<T> parser, Func<T, U> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(Scanner scanner, out ParseResult<U> result)
        {
            if (_parser.Parse(scanner, out var parsed))
            {
                var value = _action.Invoke(parsed.GetValue());
                result = new ParseResult<U>(parsed.Buffer, parsed.Start, parsed.End, value);

                return true;
            }

            result = ParseResult<U>.Empty;
            return false;
        }
    }
}
