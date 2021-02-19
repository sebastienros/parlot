using System;

namespace Parlot.Fluent
{
    public sealed class PatternLiteral : Parser<TextSpan>
    {
        private readonly Func<char, bool> _predicate;
        private readonly int _minSize;
        private readonly int _maxSize;
        private readonly bool _skipWhiteSpace;

        public PatternLiteral(Func<char, bool> predicate, int minSize = 1, int maxSize = 0, bool skipWhiteSpace = true)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _minSize = minSize;
            _maxSize = maxSize;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            if (context.Scanner.Cursor.Eof || !_predicate(context.Scanner.Cursor.Current))
            {
                return false;
            }

            var startPosition = context.Scanner.Cursor.Position;
            var start = startPosition.Offset;

            context.Scanner.Cursor.Advance();
            var found = 1;

            while (!context.Scanner.Cursor.Eof && (_maxSize > 0 ? found < _maxSize : true) && _predicate(context.Scanner.Cursor.Current))
            {
                context.Scanner.Cursor.Advance();
                found++;
            }

            if (found >= _minSize)
            {
                var end = context.Scanner.Cursor.Offset;
                result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));

                return true;
            }

            // When the size constraint has not been met the parser may still have advanced the cursor.
            context.Scanner.Cursor.ResetPosition(startPosition);            

            return false;
        }
    }
}
