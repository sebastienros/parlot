namespace Parlot.Fluent
{
    public sealed class WhiteSpaceLiteral : Parser<TextSpan>
    {
        private readonly bool _includeNewLines;
        private readonly bool _failOnEmpty;

        public WhiteSpaceLiteral(bool includeNewLines, bool failOnEmpty)
        {
            _includeNewLines = includeNewLines;
            _failOnEmpty = failOnEmpty;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Offset;

            if (_includeNewLines)
            {
                context.Scanner.SkipWhiteSpaceOrNewLine();
            }
            else
            {
                context.Scanner.SkipWhiteSpace();
            }

            var end = context.Scanner.Cursor.Offset;

            if (_failOnEmpty && start == end)
            {
                return false;
            }

            result.Set(start, context.Scanner.Cursor.Offset,  new TextSpan(context.Scanner.Buffer, start, end - start));
            return true;
        }
    }
}
