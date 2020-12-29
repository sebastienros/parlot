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

        public override bool Parse(Scanner scanner, ref ParseResult<char> result)
        {
            if (SkipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var start = scanner.Cursor.Position;

            if (scanner.ReadChar(Char))
            {
                result.Set(scanner.Buffer, start, scanner.Cursor.Position, Char);
                return true;
            }

            return false;
        }
    }
}
