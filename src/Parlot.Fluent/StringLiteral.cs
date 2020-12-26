namespace Parlot.Fluent
{
    public class StringLiteral : Parser<string>
    { 
        public StringLiteral(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public override bool Parse(Scanner scanner, IParseResult<string> result)
        {
            var token = result == null ? null : new TokenResult();

            if(scanner.ReadText(Text, token))
            {
                result?.Succeed(token.Buffer, token.Start, token.End, Text);
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
