﻿using System;
using System.Globalization;
using System.Text;

namespace Parlot.Fluent
{
    public sealed class IntegerLiteral : Parser<long>
    {
        private readonly NumberOptions _numberOptions;
        private readonly bool _skipWhiteSpace;

        public IntegerLiteral(NumberOptions numberOptions = NumberOptions.Default, bool skipWhiteSpace = true)
        {
            _numberOptions = numberOptions;
            _skipWhiteSpace = skipWhiteSpace;
        }
        public override bool Parse(ParseContext context, ref ParseResult<long> result)
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

            if (context.Scanner.ReadInteger())
            {
                var end = context.Scanner.Cursor.Offset;

#if NETSTANDARD2_0
                var sourceToParse = context.Scanner.Buffer.ToString();
#else
                var sourceToParse = context.Scanner.Buffer.AsSpan(start, end - start);
#endif

                if (long.TryParse(sourceToParse, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                {
                    result.Set(start, end,  value);
                    return true;
                }
            }
         
            return false;
        }
    }
}
