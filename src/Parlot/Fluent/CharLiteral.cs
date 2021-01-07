namespace Parlot.Fluent
{
    public sealed class CharLiteral : Parser<char>
    {
        public CharLiteral(char c, bool skipWhiteSpace = true)
        {
            Char = c;
            SkipWhiteSpace = skipWhiteSpace;
        }

        public char Char { get; }

        public bool SkipWhiteSpace { get; }

        public override bool Parse(ParseContext context, ref ParseResult<char> result)
        {
            context.EnterParser(this);

            if (SkipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            if (context.Scanner.ReadChar(Char))
            {
                result.Set(context.Scanner.Buffer, start, context.Scanner.Cursor.Offset, Name, Char);
                return true;
            }

            return false;
        }
    }
}
