using System;

namespace Parlot.Fluent
{
    public sealed class Identifier<TParseContext> : Parser<TextSpan, TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Func<char, bool> _extraStart;
        private readonly Func<char, bool> _extraPart;
        private readonly bool _skipWhiteSpace;

        public Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null, bool skipWhiteSpace = true)
        {
            _extraStart = extraStart;
            _extraPart = extraPart;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(TParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var first = context.Scanner.Cursor.Current;

            if (!Character.IsIdentifierStart(first) && (_extraStart == null || !_extraStart(first)))
            {
                return false;
            }

            var start = context.Scanner.Cursor.Offset;

            // At this point we have an identifier, read while it's an identifier part.

            context.Scanner.Cursor.Advance();

            while (!context.Scanner.Cursor.Eof && (Character.IsIdentifierPart(context.Scanner.Cursor.Current) || (_extraPart != null && _extraPart(context.Scanner.Cursor.Current))))
            {
                context.Scanner.Cursor.Advance();
            }

            var end = context.Scanner.Cursor.Offset;

            result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));
            return true;
        }
    }
}
