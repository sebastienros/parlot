namespace Parlot.Fluent
{
    public class CharTerminal : IParser<TokenResult>
    {
        public CharTerminal(char c)
        {
            Char = c;
        }

        public char Char { get; }

        public bool Parse(Scanner scanner, IParseResult<TokenResult> result)
        {
            var token = result == null ? null : new TokenResult();

            if (scanner.ReadChar(Char, token))
            {
                result?.Succeed(token.Buffer, token.Start, token.End, token);
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
