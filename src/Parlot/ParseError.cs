namespace Parlot
{
    public class ParseError
    {
        public ParseError(ParseException parseException)
            : this(parseException.Message, parseException.Position)
        {
        }

        public ParseError(string message, in TextPosition position)
        {
            Message = message;
            Position = position;
        }

        public string Message { get; }
        public TextPosition Position { get; }
    }
}
