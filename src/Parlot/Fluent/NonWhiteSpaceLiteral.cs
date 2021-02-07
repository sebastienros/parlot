namespace Parlot.Fluent
{
    public sealed class NonWhiteSpaceLiteral : Parser<TextSpan>
    {
        private readonly bool _skipWhiteSpace;
        private readonly bool _includeNewLines;

        public NonWhiteSpaceLiteral(bool skipWhiteSpace = true, bool includeNewLines = false)
        {
            _skipWhiteSpace = skipWhiteSpace;
            _includeNewLines = includeNewLines;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            if (context.Scanner.Cursor.Eof)
            {
                return false;
            }

            var start = context.Scanner.Cursor.Offset;

            if (_includeNewLines)
            {
                context.Scanner.ReadNonWhiteSpace();
            }
            else
            {
                context.Scanner.ReadNonWhiteSpaceOrNewLine();
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
