namespace Parlot.Fluent
{
    public class StringTerminal : IParser<TokenResult>
    {
        public StringTerminal(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public bool Parse(Scanner scanner, IParseResult<TokenResult> result)
        {
            var token = result == null ? null : new TokenResult();

            if(scanner.ReadText(Text, token))
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
