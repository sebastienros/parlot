namespace Parlot.Fluent
{
    public class DecimalConversion : Parser<decimal>
    {
        private readonly IParser<TokenResult> _parser;

        public DecimalConversion(IParser<TokenResult> parser)
        {
            _parser = parser;
        }

        public override bool Parse(Scanner scanner, out ParseResult<decimal> result)
        {
            if (_parser.Parse(scanner, out var tokenResult))
            {
                decimal.TryParse(tokenResult.GetValue().Span, out var value);
                result = new ParseResult<decimal>(tokenResult.Buffer, tokenResult.Start, tokenResult.End, value);

                return true;
            }

            result = ParseResult<decimal>.Empty;
            return false;
        }
    }
}
