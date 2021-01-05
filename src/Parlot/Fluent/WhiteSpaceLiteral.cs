namespace Parlot.Fluent
{
    public sealed class WhiteSpaceLiteral : Parser<TextSpan>
    {
        private readonly bool _includeNewLines;

        public WhiteSpaceLiteral(bool includeNewLines)
        {
            _includeNewLines = includeNewLines;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            if (_includeNewLines)
            {
                context.Scanner.SkipWhiteSpaceOrNewLine();
            }
            else
            {
                context.Scanner.SkipWhiteSpace();
            }

            var end = context.Scanner.Cursor.Position;

            result.Set(context.Scanner.Buffer, start, context.Scanner.Cursor.Position, Name, new TextSpan(context.Scanner.Buffer, start.Offset, end - start));
            return true;
        }
    }
}
