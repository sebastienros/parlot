namespace Parlot.Fluent
{
    public sealed class NonWhiteSpaceLiteral : Parser<TextSpan>
    {
        private readonly bool _skipWhiteSpace;

        public NonWhiteSpaceLiteral(bool skipWhiteSpace = true)
        {
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            if (_skipWhiteSpace)
            {
                context.Scanner.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            while (!context.Scanner.Cursor.Eof && context.Scanner.ReadNonWhiteSpace())
            {
                context.Scanner.Cursor.Advance();
            }

            var end = context.Scanner.Cursor.Offset;

            if (start == end)
            {
                return false;
            }

            result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));
            return true;
        }
    }
}
