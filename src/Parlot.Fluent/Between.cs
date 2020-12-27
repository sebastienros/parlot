using System;

namespace Parlot.Fluent
{
    public class Between<T> : Parser<T>
    {
        private readonly IParser<T> _parser;
        private readonly string _before;
        private readonly string _after;
        private readonly bool _skipWhiteSpace;

        public Between(string before, IParser<T> parser, string after, bool skipWhiteSpace = true)
        {
            _before = before ?? throw new ArgumentNullException(nameof(before));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _after = after ?? throw new ArgumentNullException(nameof(after));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<T> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var start = scanner.Cursor.Position;

            if (scanner.ReadText(_before))
            {
                if (_parser.Parse(scanner, out var parsed))
                {
                    if (_skipWhiteSpace)
                    {
                        scanner.SkipWhiteSpace();
                    }

                    if (scanner.ReadText(_after))
                    {
                        result = new ParseResult<T>(parsed.Buffer, start, parsed.End, parsed.GetValue());
                        return true;
                    }
                }
            }

            result = ParseResult<T>.Empty;
            return false;
        }
    }
}
