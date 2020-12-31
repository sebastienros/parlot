using System;

namespace Parlot.Fluent
{
    public sealed class IntegerLiteral : Parser<long>
    {
        private readonly bool _skipWhiteSpace;

        public IntegerLiteral(bool skipWhiteSpace = true)
        {
            _skipWhiteSpace = skipWhiteSpace;
        }
        public override bool Parse(ParseContext context, ref ParseResult<long> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Position;

            if (context.Scanner.ReadDecimal())
            {
                var end = context.Scanner.Cursor.Position;

                if (long.TryParse(context.Scanner.Buffer.AsSpan(start.Offset, end - start), out var value))
                {
                    result.Set(context.Scanner.Buffer, start, end, value);
                    return true;
                }
            }
         
            return false;
        }
    }
}
