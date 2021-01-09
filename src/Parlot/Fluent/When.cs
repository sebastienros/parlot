using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Ensure the given parser is valid based on a condition
    /// </summary>
    /// <typeparam name="T">The output parser type.</typeparam>
    public sealed class When<T> : Parser<T>
    {
        private readonly Func<T, bool> _action;
        private readonly Parser<T> _parser;

        public When(Parser<T> parser, Func<T, bool> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);
            
            return _parser.Parse(context, ref result) && _action(result.Value);
        }
    }
}
