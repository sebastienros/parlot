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
        public override bool Parse(Scanner scanner, out ParseResult<long> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var start = scanner.Cursor.Position;

            if (scanner.ReadDecimal())
            {
                var end = scanner.Cursor.Position;

                if (long.TryParse(scanner.Buffer.AsSpan(start.Offset, end - start), out var value))
                {
                    result = new ParseResult<long>(scanner.Buffer, start, end, value);
                    return true;
                }
            }
         
            result = ParseResult<long>.Empty;
            return false;
        }
    }
}
