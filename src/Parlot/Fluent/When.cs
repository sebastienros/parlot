using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// Ensure the given parser is valid based on a condition, and backtracks if not.
    /// </summary>
    /// <typeparam name="T">The output parser type.</typeparam>
    /// <typeparam name="TParseContext">The parse context type.</typeparam>
    public sealed class When<T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Func<T, bool> _action;
        private readonly IParser<T, TParseContext> _parser;

        public When(IParser<T, TParseContext> parser, Func<T, bool> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            var valid = _parser.Parse(context, ref result) && _action(result.Value);

            if (!valid)
            {
                context.Scanner.Cursor.ResetPosition(start);
            }

            return valid;
        }
    }
}
