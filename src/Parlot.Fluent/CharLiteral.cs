namespace Parlot.Fluent
{
    public class CharLiteral : Parser<char>
    {
        public CharLiteral(char c)
        {
            Char = c;
        }

        public char Char { get; }

        public override bool Parse(Scanner scanner, out ParseResult<char> result)
        {
            var start = scanner.Cursor.Position;

            if (scanner.ReadChar(Char))
            {
                result = new ParseResult<char>(scanner.Buffer, start, scanner.Cursor.Position, Char);
                return true;
            }

            result = ParseResult<char>.Empty;
            return false;
        }
    }
}
