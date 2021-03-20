using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Routes the parsing based on a custom delegate.
    /// </summary>
    public sealed class Switch<T, U, TParseContext> : Parser<U, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<T, TParseContext> _previousParser;
        private readonly Func<TParseContext, T, Parser<U, TParseContext>> _action;
        public Switch(IParser<T, TParseContext> previousParser, Func<TParseContext, T, Parser<U, TParseContext>> action)
        {
            _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool Parse(TParseContext context, ref ParseResult<U> result)
        {
            var previousResult = new ParseResult<T>();

            if (!_previousParser.Parse(context, ref previousResult))
            {
                return false;
            }

            var nextParser = _action(context, previousResult.Value);

            if (nextParser == null)
            {
                return false;
            }

            var parsed = new ParseResult<U>();

            if (nextParser.Parse(context, ref parsed))
            {
                result.Set(parsed.Start, parsed.End, parsed.Value);
                return true;
            }

            return false;
        }
    }
}
