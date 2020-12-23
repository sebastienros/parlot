namespace Parlot
{
    public abstract class Parser<T>
    {
        public abstract T Parse(string text);

        public bool TryParse(string text, out T expression, out ParseError error)
        {
            error = null;
            expression = default;

            try
            {
                expression = Parse(text);

                return true;
            }
            catch (ParseException e)
            {
                error = new ParseError
                {
                    Message = e.Message,
                    Position = e.Position
                };
            }

            return false;
        }
    }
}
