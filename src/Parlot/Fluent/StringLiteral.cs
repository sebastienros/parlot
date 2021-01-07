using System;

namespace Parlot.Fluent
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
                var encoded = context.Scanner.Buffer.AsSpan(start + 1, end - start - 2);
                var decoded = Character.DecodeString(encoded);

                // Don't create a new string if the decoded string is the same, meaning is 
                // has no escape sequences.
                var span = decoded == encoded || decoded.SequenceEqual(encoded)
                    ? new TextSpan(context.Scanner.Buffer, start + 1, encoded.Length)
                    : new TextSpan(decoded.ToString());

                result.Set(context.Scanner.Buffer, start, end, Name, span);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
