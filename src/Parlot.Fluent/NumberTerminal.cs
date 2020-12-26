namespace Parlot.Fluent
{
    public class NumberTerminal : IParser<TokenResult>
    {
        public bool Parse(Scanner scanner, IParseResult<TokenResult> result)
        {
            var token = result == null ? null : new TokenResult();

            if (scanner.ReadDecimal(token))
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
