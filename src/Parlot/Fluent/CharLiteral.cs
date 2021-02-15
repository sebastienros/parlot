using System;

namespace Parlot.Fluent
{
    public sealed class CharLiteral : Parser<char>
    {
        private readonly Func<char, bool> _predicate;

        public CharLiteral(char c, bool skipWhiteSpace = true)
        {
            Char = c;
            SkipWhiteSpace = skipWhiteSpace;
        }

        public CharLiteral(Func<char, bool> predicate, bool skipWhiteSpace = true)
        {
            _predicate = predicate;
            SkipWhiteSpace = skipWhiteSpace;            
        }

        public char Char { get; }

        public bool SkipWhiteSpace { get; }

        public override bool Parse(ParseContext context, ref ParseResult<char> result)
        {
            context.EnterParser(this);

            if (SkipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            if (_predicate != null && _predicate(context.Scanner.Cursor.Current))
            {
                result.Set(start, context.Scanner.Cursor.Offset, context.Scanner.Cursor.Current);
                context.Scanner.Cursor.Advance();

                return true;
            }

            if (context.Scanner.ReadChar(Char))
            {
                result.Set(start, context.Scanner.Cursor.Offset, Char);
                return true;
            }

            return false;
        }
    }
}
