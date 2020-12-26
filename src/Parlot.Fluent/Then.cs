using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Returns a new <see cref="IParser{TResult}" /> converting the input value of 
    /// type <see cref="T"/> to the output value of type <see cref="U"/> using a custom function.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="U">The output parser type.</typeparam>
    public class Then<T, U> : Parser<U>
    {
        private readonly Func<T, U> _action;
        private readonly IParser<T> _parser;

        public Then(IParser<T> parser, Func<T, U> action)
        {
            _action = action;
            _parser = parser;
        }

        public override bool Parse(Scanner scanner, IParseResult<U> result)
        {
            var localResult = result != null ? new ParseResult<T>() : null;
            if (_parser.Parse(scanner, localResult))
            {
                if (localResult != null && localResult.Success)
                {
                    var value = _action != null ? _action.Invoke(localResult.GetValue()) : default;
                    result?.Succeed(localResult.Buffer, localResult.Start, localResult.End, value);
                }

                return true;
            }

            return false;
        }
    }
}
