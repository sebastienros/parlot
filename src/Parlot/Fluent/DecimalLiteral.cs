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

            var start = context.Scanner.Cursor.Offset;

            if ((_numberOptions & NumberOptions.AllowSign) == NumberOptions.AllowSign)
            {
                if (!context.Scanner.ReadChar('-'))
                {
                    // If there is no '-' try to read a '+' but don't read both.
                    context.Scanner.ReadChar('+');
                }
            }

            if (context.Scanner.ReadDecimal())
            {
                var end = context.Scanner.Cursor.Offset;
#if NETSTANDARD2_0
                var sourceToParse = context.Scanner.Buffer.ToString();
#else
                var sourceToParse = context.Scanner.Buffer.AsSpan(start, end - start);
#endif

                if (decimal.TryParse(sourceToParse, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                { 
                    result.Set(start, end,  value);
                    return true;
                }
            }
         
            return false;
        }
    }
}
