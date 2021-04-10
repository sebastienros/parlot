using System;

namespace Parlot.Fluent
{
    using Compilation;
    using System.Linq.Expressions;

    /// <summary>
    /// Routes the parsing based on a custom delegate.
    /// </summary>
    public sealed class Switch<T, U> : Parser<U>
    {
        // This parser doesn't implement ICompilable since the returned parser can't be compiled preemptively.
        
        private readonly Parser<T> _previousParser;
        private readonly Func<ParseContext, T, Parser<U>> _action;
        public Switch(Parser<T> previousParser, Func<ParseContext, T, Parser<U>> action)
        {
            _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
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
