using System;

namespace Parlot.Fluent
{
    public class DecimalLiteral : Parser<decimal>
    {
        public override bool Parse(Scanner scanner, out ParseResult<decimal> result)
        {
            var start = scanner.Cursor.Position;

            if (scanner.ReadDecimal())
            {
                var end = scanner.Cursor.Position;

                if (decimal.TryParse(scanner.Buffer.AsSpan(start.Offset, end - start), out var value))
                {
                    result = new ParseResult<decimal>(scanner.Buffer, start, end, value);
                    return true;
                }
            }
         
            result = ParseResult<decimal>.Empty;
            return false;
        }
    }
}
