namespace Parlot.Fluent
{
    public abstract partial class Parser<T>
    {
        public T Parse(string text, ParseContext context = null)
        {
            context ??= new ParseContext(new Scanner(text));

            var localResult = new ParseResult<T>();

            var success = Parse(context, ref localResult);

            if (success)
            {
                return localResult.Value;
            }

            return default;
        }

        public bool TryParse(string text, out T value)
        {
            return TryParse(text, out value, out _);
        }

        public bool TryParse(string text, out T value, out ParseError error)
        {
            return TryParse(new ParseContext(new Scanner(text)), out value, out error);
        }

        public bool TryParse(ParseContext context, out T value, out ParseError error)
        {
            error = null;

            try
            {
                var localResult = new ParseResult<T>();

                var success = this.Parse(context, ref localResult);

                if (success)
                {
                    value = localResult.Value;
                    return true;
                }
            }
            catch (ParseException e)
            {
                error = new ParseError
                {
                    Message = e.Message,
                    Position = e.Position
                };
            }

            value = default;
            return false;
        }
    }
}
