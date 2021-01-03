using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Routes the parsing based on a custom delegate.
    /// </summary>
    public sealed class SwitchAnonymousToAnonymous : Parser<object>
    {
        private readonly IParser _previousParser;
        private readonly Func<ParseContext, object, IParser> _action;
        public SwitchAnonymousToAnonymous(IParser previousParser, Func<ParseContext, object, IParser> action)
        {
            _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool Parse(ParseContext context, ref ParseResult<object> result)
        {
            var previousResult = new ParseResult<object>();

            if (!_previousParser.Parse(context, ref previousResult))
            {
                return false;
            }
            
            var nextParser = _action(context, previousResult.Value);

            if (nextParser == null)
            {
                return false;
            }

            var parsed = new ParseResult<object>();

            if (nextParser.Parse(context, ref parsed))
            {
                result.Set(parsed.Buffer, parsed.Start, parsed.End, nextParser.Name, parsed.Value);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Routes the parsing based on a custom delegate.
    /// </summary>
    public sealed class SwitchAnonymousToTyped<U> : Parser<U>
    {
        private readonly IParser _previousParser;
        private readonly Func<ParseContext, object, IParser<U>> _action;
        public SwitchAnonymousToTyped(IParser previousParser, Func<ParseContext, object, IParser<U>> action)
        {
            _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
        {
            var previousResult = new ParseResult<object>();

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
                result.Set(parsed.Buffer, parsed.Start, parsed.End, nextParser.Name, parsed.Value);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Routes the parsing based on a custom delegate.
    /// </summary>
    public sealed class SwitchTypedToTyped<T, U> : Parser<U>
    {
        private readonly IParser<T> _previousParser;
        private readonly Func<ParseContext, T, IParser<U>> _action;
        public SwitchTypedToTyped(IParser<T> previousParser, Func<ParseContext, T, IParser<U>> action)
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
                result.Set(parsed.Buffer, parsed.Start, parsed.End, nextParser.Name, parsed.Value);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Routes the parsing based on a custom delegate.
    /// </summary>
    public sealed class SwitchTypedToAnonymous<T> : Parser<object>
    {
        private readonly IParser<T> _previousParser;
        private readonly Func<ParseContext, T, IParser> _action;
        public SwitchTypedToAnonymous(IParser<T> previousParser, Func<ParseContext, T, IParser> action)
        {
            _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool Parse(ParseContext context, ref ParseResult<object> result)
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

            var parsed = new ParseResult<object>();

            if (nextParser.Parse(context, ref parsed))
            {
                result.Set(parsed.Buffer, parsed.Start, parsed.End, nextParser.Name, parsed.Value);
                return true;
            }

            return false;
        }
    }
}
