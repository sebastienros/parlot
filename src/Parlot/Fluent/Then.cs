using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Returns a new <see cref="Parser{U,TParseContext}" /> converting the input value of 
    /// type T to the output value of type U using a custom function.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="U">The output parser type.</typeparam>
    /// <typeparam name="TParseContext">The parse context type.</typeparam>
    public sealed class Then<T, U, TParseContext> : Parser<U, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Func<T, U> _transform1;
        private readonly Func<TParseContext, T, U> _transform2;
        private readonly IParser<T, TParseContext> _parser;

        public Then(IParser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(IParser<T, TParseContext> parser, Func<T, U> action)
        {
            _transform1 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(IParser<T, TParseContext> parser, Func<TParseContext, T, U> action)
        {
            _transform2 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<U> result)
        {
            context.EnterParser(this);

            var parsed = new ParseResult<T>();

            if (_parser.Parse(context, ref parsed))
            {
                if (_transform1 != null)
                {
                    result.Set(parsed.Start, parsed.End, _transform1(parsed.Value));
                }
                else if (_transform2 != null)
                {
                    result.Set(parsed.Start, parsed.End, _transform2(context, parsed.Value));
                }

                return true;
            }

            return false;
        }
    }


    /// <summary>
    /// Returns a new <see cref="Parser{U,TParseContext}" /> converting the input value of 
    /// type T to the output value of type U using a custom function.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="TParseContext">The parse context type.</typeparam>
    public sealed class Then<T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Action<T> _action1;
        private readonly Action<TParseContext, T> _action2;
        private readonly IParser<T, TParseContext> _parser;

        public Then(IParser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(IParser<T, TParseContext> parser, Action<T> action)
        {
            _action1 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(IParser<T, TParseContext> parser, Action<TParseContext, T> action)
        {
            _action2 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (_parser.Parse(context, ref result))
            {
                if (_action1 != null)
                {
                    _action1(result.Value);
                }
                else if (_action2 != null)
                {
                    _action2(context, result.Value);
                }

                return true;
            }

            return false;
        }
    }
}
