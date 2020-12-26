namespace Parlot.Fluent
{
    public class CharLiteral : Parser<char>
    {
        public CharLiteral(char c)
        {
            Char = c;
        }

        public char Char { get; }

        public override bool Parse(Scanner scanner, IParseResult<char> result)
        {
            var token = new TokenResult();

            if (scanner.ReadChar(Char, token))
            {
                result?.Succeed(token.Buffer, token.Start, token.End, Char);
                return true;
            }
            else
            {
                result?.Fail();
                return false;
            }
        }
    }
}
