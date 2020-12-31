using System;

namespace Parlot.Fluent
{
    public sealed class DecimalLiteral : Parser<decimal>
    {
        private readonly bool _skipWhiteSpace;

        public DecimalLiteral(bool skipWhiteSpace = true)
        {
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(ParseContext context, ref ParseResult<decimal> result)
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

                if (decimal.TryParse(context.Scanner.Buffer.AsSpan(start.Offset, end - start), out var value))
                {
                    result.Set(context.Scanner.Buffer, start, end, value);
                    return true;
                }
            }
         
            return false;
        }
    }
}
