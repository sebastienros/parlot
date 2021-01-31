using System;

namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when all parsers return the same type.
    /// We then return the actual result of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class OneOf<T> : Parser<T>
    {
        private readonly Parser<T>[] _parsers;

        public OneOf(Parser<T>[] parsers)
        {
            _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
        }

        public Parser<T>[] Parsers => _parsers;

        public override bool Parse(in ParseContext context, ref ParseResult<T> result)
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

    public sealed class OneOf<A, B, T> : Parser<T>
        where A: T
        where B: T
    {
        private readonly Parser<A> _parserA;
        private readonly Parser<B> _parserB;

        public OneOf(Parser<A> parserA, Parser<B> parserB)
        {
            _parserA = parserA ?? throw new ArgumentNullException(nameof(parserA));
            _parserB = parserB ?? throw new ArgumentNullException(nameof(parserB));
        }

        public override bool Parse(in ParseContext context, ref ParseResult<T> result)
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
