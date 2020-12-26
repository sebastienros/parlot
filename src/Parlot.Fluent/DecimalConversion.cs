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
            var localResult = result != null ? new ParseResult<TokenResult>() : null;
            if (_parser.Parse(scanner, localResult))
            {
                if (localResult != null && localResult.Success)
                {
                    decimal.TryParse(localResult.GetValue().Span, out var value);
                    result?.Succeed(localResult.Buffer, localResult.Start, localResult.End, value);
                }

                return true;
            }

            return false;
        }
    }
}
