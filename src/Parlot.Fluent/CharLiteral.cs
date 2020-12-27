namespace Parlot.Fluent
{
    public class CharLiteral : Parser<char>
    {
        private readonly bool _skipWhiteSpace;

        public CharLiteral(char c, bool skipWhiteSpace = true)
        {
            Char = c;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public char Char { get; }

        public override bool Parse(Scanner scanner, out ParseResult<char> result)
        {
            if (_skipWhiteSpace)
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
