using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Returns a new <see cref="Parser{T,TParseContext}" /> converting the input parser of 
    /// type T to a scoped parsed.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="TParseContext">The parser context type.</typeparam>
    /// <typeparam name="TChar">The char type.</typeparam>
    public sealed class ScopedParser<T, TParseContext, TChar> : Parser<T, TParseContext, TChar>
    where TParseContext : ScopeParseContext<TChar, TParseContext>
    where TChar : IEquatable<TChar>, IConvertible
    {
        private readonly Action<TParseContext> _action;
        private readonly Parser<T, TParseContext> _parser;

        public ScopedParser(Action<TParseContext> action, Parser<T, TParseContext> parser)
        {
            _action = action;
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public ScopedParser(Parser<T, TParseContext> parser)
        : this(null, parser)
        {
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);
            context = context.Scope();
            if (_action != null)
                _action(context);
            return _parser.Parse(context, ref result);
        }
    }
}
