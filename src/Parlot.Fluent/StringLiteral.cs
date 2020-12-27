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

        public override bool Parse(Scanner scanner, out ParseResult<TextSpan> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var start = scanner.Cursor.Position;

            var token = new TokenResult();
            var success = _quotes switch
            {
                StringLiteralQuotes.Single => scanner.ReadSingleQuotedString(token),
                StringLiteralQuotes.Double => scanner.ReadDoubleQuotedString(token),
                StringLiteralQuotes.SingleOrDouble => scanner.ReadQuotedString(token),
                _ => false
            };

            if (success)
            {
                var decoded = Character.DecodeString(token.Span);
                
                // Don't create a new string if the decoded string is the same, meaning is 
                // has no escape sequences.
                var span = decoded == token.Span 
                    ? new TextSpan(scanner.Buffer, start.Offset, decoded.Length)
                    : new TextSpan(token.Text);

                result = new ParseResult<TextSpan>(scanner.Buffer, start, scanner.Cursor.Position, span);
                return true;
            }
            else
            {
                result = ParseResult<TextSpan>.Empty;
                return false;
            }
        }
    }
}
