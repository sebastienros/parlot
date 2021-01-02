using System;
using System.Globalization;

namespace Parlot.Fluent
{
    public sealed class DecimalLiteral : Parser<decimal>
    {
        private readonly NumberOptions _numberOptions;
        private readonly bool _skipWhiteSpace;

        public DecimalLiteral(NumberOptions numberOptions = NumberOptions.Default, bool skipWhiteSpace = true)
        {
            _numberOptions = numberOptions;
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

            if ((_numberOptions & NumberOptions.AllowSign) == NumberOptions.AllowSign)
            {
                context.Scanner.ReadChar('-');
            }

            if (context.Scanner.ReadDecimal())
            {
                var end = context.Scanner.Cursor.Position;

                if (decimal.TryParse(context.Scanner.Buffer.AsSpan(start.Offset, end - start), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                {
                    result.Set(context.Scanner.Buffer, start, end, Name, value);
                    return true;
                }
            }
         
            return false;
        }
    }
}
