namespace Parlot.Fluent
{
    public class NumberLiteral : Parser<TokenResult>
    {
        public override bool Parse(Scanner scanner, out ParseResult<TokenResult> result)
        {
            var token = new TokenResult();

            if (scanner.ReadDecimal(token))
            {
                result = new ParseResult<TokenResult>(token.Buffer, token.Start, token.End, token);
                return true;
            }
            else
            {
                result = ParseResult<TokenResult>.Empty;
                return false;
            }
        }
    }
}
