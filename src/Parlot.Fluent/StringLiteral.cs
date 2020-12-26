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
            var start = scanner.Cursor.Position;

            if (scanner.ReadText(Text))
            {
                result = new ParseResult<string>(scanner.Buffer, start, scanner.Cursor.Position, Text);
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
