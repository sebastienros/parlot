namespace Parlot.Fluent
{
    public class DecimalConversion : Parser<decimal>
    {
        private readonly IParser<TokenResult> _parser;

        public DecimalConversion(IParser<TokenResult> parser)
        {
            _parser = parser;
        }

        public override bool Parse(Scanner scanner, IParseResult<decimal> result)
        {
            var tokenResult = new ParseResult<TokenResult>();

            if (_parser.Parse(scanner, tokenResult))
            {
                decimal.TryParse(tokenResult.GetValue().Span, out var value);
                result?.Succeed(tokenResult.Buffer, tokenResult.Start, tokenResult.End, value);

                return true;
            }

            result?.Fail();
            return false;
        }
    }
}
