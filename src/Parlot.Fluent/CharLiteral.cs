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
            var token = new TokenResult();

            if (scanner.ReadChar(Char, token))
            {
                result = new ParseResult<char>(token.Buffer, token.Start, token.End, Char);
                return true;
            }

            result = ParseResult<char>.Empty;
            return false;
        }
    }
}
