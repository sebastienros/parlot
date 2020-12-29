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

        public override bool Parse(Scanner scanner, ref ParseResult<TextSpan> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var start = scanner.Cursor.Position;

            var success = _quotes switch
            {
                StringLiteralQuotes.Single => scanner.ReadSingleQuotedString(),
                StringLiteralQuotes.Double => scanner.ReadDoubleQuotedString(),
                StringLiteralQuotes.SingleOrDouble => scanner.ReadQuotedString(),
                _ => false
            };

            var end = scanner.Cursor.Position;

            if (success)
            {
                // Remove quotes
                var encoded = scanner.Buffer.AsSpan(start.Offset + 1, end - start - 2);
                var decoded = Character.DecodeString(encoded);

                // Don't create a new string if the decoded string is the same, meaning is 
                // has no escape sequences.
                var span = decoded == encoded || decoded.SequenceEqual(encoded)
                    ? new TextSpan(scanner.Buffer, start.Offset + 1, encoded.Length)
                    : new TextSpan(decoded.ToString());

                result.Set(scanner.Buffer, start, end, span);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
