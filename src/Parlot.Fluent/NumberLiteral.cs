namespace Parlot.Fluent
{
    public class NumberLiteral : Parser<TokenResult>
    {
        public override bool Parse(Scanner scanner, IParseResult<TokenResult> result)
        {
            var token = new TokenResult();

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
