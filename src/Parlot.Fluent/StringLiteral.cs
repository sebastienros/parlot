namespace Parlot.Fluent
{
    public class StringLiteral : Parser<string>
    { 
        public StringLiteral(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public override bool Parse(Scanner scanner, out ParseResult<string> result)
        {
            var token = new TokenResult();

            if(scanner.ReadText(Text, token))
            {
                result = new ParseResult<string>(token.Buffer, token.Start, token.End, Text);
                return true;
            }
            else
            {
                result = ParseResult<string>.Empty;
                return false;
            }
        }
    }
}
