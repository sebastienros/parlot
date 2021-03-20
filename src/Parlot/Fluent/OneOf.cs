using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when all parsers return the same type.
    /// We then return the actual result of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TParseContext"></typeparam>
    public sealed class OneOf<T, TParseContext> : Parser<T, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<T, TParseContext>[] _parsers;

        public OneOf(IParser<T, TParseContext>[] parsers)
        {
            _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
        }

        public IParser<T, TParseContext>[] Parsers => _parsers;

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            foreach (var parser in _parsers)
            {
                if (parser.Parse(context, ref result))
                {
                    return true;
                }

                // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }
    }

    public sealed class OneOf<A, B, T, TParseContext> : Parser<T, TParseContext>
        where A : T
        where B : T
        where TParseContext : ParseContext
    {
        private readonly IParser<A, TParseContext> _parserA;
        private readonly IParser<B, TParseContext> _parserB;

        public OneOf(IParser<A, TParseContext> parserA, IParser<B, TParseContext> parserB)
        {
            _parserA = parserA ?? throw new ArgumentNullException(nameof(parserA));
            _parserB = parserB ?? throw new ArgumentNullException(nameof(parserB));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);


            var resultA = new ParseResult<A>();

            var start = context.Scanner.Cursor.Position;

            if (_parserA.Parse(context, ref resultA))
            {
                result.Set(resultA.Start, resultA.End, resultA.Value);

                return true;
            }

            // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
            context.Scanner.Cursor.ResetPosition(start);

            var resultB = new ParseResult<B>();

            if (_parserB.Parse(context, ref resultB))
            {
                result.Set(resultA.Start, resultA.End, resultA.Value);

                return true;
            }

            // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }
    }
}
