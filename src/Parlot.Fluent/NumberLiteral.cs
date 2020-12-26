namespace Parlot.Fluent
{
    public class NumberLiteral : Parser<TextSpan>
    {
        public override bool Parse(Scanner scanner, out ParseResult<TextSpan> result)
        {
            var start = scanner.Cursor.Position;

            if (scanner.ReadDecimal())
            {
                var end = scanner.Cursor.Position;

                result = new ParseResult<TextSpan>(scanner.Buffer, start, end, new TextSpan(scanner.Buffer, start.Offset, end - start));
                return true;
            }
            else
            {
                result = ParseResult<TextSpan>.Empty;
                return false;
            }
        }
    }
}
