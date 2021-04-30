namespace Parlot.Fluent
{
        
    public static class ParserExtensions
    {
        public static T Parse<T, TParseContext>(this IParser<T, TParseContext> parser, string text, TParseContext context)
        where TParseContext : ParseContext
        {
            var localResult = new ParseResult<T>();

            var success = parser.Parse(context, ref localResult);

            if (success)
            {
                return localResult.Value;
            }

            return default;
        }
    
        public static T Parse<T>(this IParser<T, ParseContext> parser, string text)
        {
            return parser.Parse(text, new ParseContext(new Scanner(text)));
        }

        public static bool TryParse<TResult>(this IParser<TResult, ParseContext> parser, string text, out TResult value)
        {
            return parser.TryParse(text, out value, out _);
        }

        public static bool TryParse<TResult>(this IParser<TResult, ParseContext> parser, string text, out TResult value, out ParseError error)
        {
            return TryParse(parser, new ParseContext(new Scanner(text)), out value, out error);
        }

        public static bool TryParse<TResult, TParseContext>(this IParser<TResult, TParseContext> parser, TParseContext context, out TResult value)
        where TParseContext : ParseContext
        {
            return parser.TryParse(context, out value, out _);
        }

        
        public static bool TryParse<TResult, TParseContext>(this IParser<TResult, TParseContext> parser, TParseContext context, out TResult value, out ParseError error)
        where TParseContext : ParseContext
        {
            error = null;

            try
            {
                var localResult = new ParseResult<TResult>();

                var success = parser.Parse(context, ref localResult);

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
