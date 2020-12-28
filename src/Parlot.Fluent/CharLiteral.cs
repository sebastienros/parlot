namespace Parlot.Fluent
{
    public sealed class CharLiteral : Parser<char>
    {
        public CharLiteral(char c, bool skipWhiteSpace = true)
        {
            Char = c;
            SkipWhiteSpace = skipWhiteSpace;
        }

        public char Char { get; }

        public bool SkipWhiteSpace { get; }

        public override bool Parse(Scanner scanner, out ParseResult<char> result)
        {
            if (SkipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

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
