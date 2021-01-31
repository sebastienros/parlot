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

        public override bool Parse(in ParseContext context, ref ParseResult<string> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            if (context.Scanner.ReadText(Text, _comparer))
            {
                result.Set(start, context.Scanner.Cursor.Offset,  Text);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
