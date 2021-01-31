﻿namespace Parlot.Fluent
{
    public enum StringLiteralQuotes
    {
        Single,
        Double,
        SingleOrDouble
    }

    public sealed class StringLiteral : Parser<TextSpan>
    {
        private readonly StringLiteralQuotes _quotes;
        private readonly bool _skipWhiteSpace;

        public StringLiteral(StringLiteralQuotes quotes, bool skipWhiteSpace = true)
        {
            _quotes = quotes;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            var success = _quotes switch
            {
                StringLiteralQuotes.Single => context.Scanner.ReadSingleQuotedString(),
                StringLiteralQuotes.Double => context.Scanner.ReadDoubleQuotedString(),
                StringLiteralQuotes.SingleOrDouble => context.Scanner.ReadQuotedString(),
                _ => false
            };

            var end = context.Scanner.Cursor.Offset;

            if (success)
            {
                // Remove quotes
                var decoded = Character.DecodeString(new TextSpan(context.Scanner.Buffer, start + 1, end - start - 2));

                result.Set(start, end, decoded);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
