using System;

namespace Parlot.Fluent
{
    public sealed class TextLiteral : Parser<string>
    {
        private readonly StringComparer _comparer;
        private readonly bool _skipWhiteSpace;

        public TextLiteral(string text, StringComparer comparer = null,  bool skipWhiteSpace = true)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            _comparer = comparer;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public string Text { get; }

        public override bool Parse(Scanner scanner, ref ParseResult<string> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var start = scanner.Cursor.Position;

            if (scanner.ReadText(Text, _comparer))
            {
                result.Set(scanner.Buffer, start, scanner.Cursor.Position, Text);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
