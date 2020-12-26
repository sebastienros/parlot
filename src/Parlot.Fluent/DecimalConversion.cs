namespace Parlot.Fluent
{
    public class DecimalConversion : IParser<decimal>
    {
        private readonly IParser<TokenResult> _parser;

        public DecimalConversion(IParser<TokenResult> parser)
        {
            _parser = parser;
        }

        public bool Parse(Scanner scanner, IParseResult<decimal> result)
        {
            var localResult = result != null ? new ParseResult<TokenResult>() : null;
            if (_parser.Parse(scanner, localResult))
            {
                if (localResult != null && localResult.Success)
                {
                    decimal.TryParse(localResult.Value.Span, out var value);
                    result?.Succeed(result.Buffer, result.Start, result.End, value);
                }

                return true;
            }

            return false;
        }
    }
}
