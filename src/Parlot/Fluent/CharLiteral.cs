namespace Parlot.Fluent
{
    public sealed class CharLiteral<TParseContext> : Parser<char, TParseContext>
    where TParseContext : ParseContext
    {
        public CharLiteral(char c, bool skipWhiteSpace = true)
        {
            Char = c;
            SkipWhiteSpace = skipWhiteSpace;
        }

        public char Char { get; }

        public bool SkipWhiteSpace { get; }

        public override bool Parse(TParseContext context, ref ParseResult<char> result)
        {
            context.EnterParser(this);

            if (SkipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            if (context.Scanner.ReadChar(Char))
            {
                result.Set(start, context.Scanner.Cursor.Offset, Char);
                return true;
            }

            return false;
        }
    }
}
