using System;

namespace Parlot
{
    public struct ParseResult<T>
    {
        public ParseResult(string buffer, TextPosition start, TextPosition end, string parserName, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            Value = value;
            ParserName = parserName;
        }

        public void Set(string buffer, TextPosition start, TextPosition end, string parserName, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            Value = value;
            ParserName = parserName;
        }

        public TextPosition Start { get; private set; }

        public TextPosition End { get; private set; }

        public int Length { get; private set; }
        public string Buffer { get; private set; }

        public string Text => Buffer?.Substring(Start.Offset, Length);

        public ReadOnlySpan<char> Span => Buffer.AsSpan(Start.Offset, Length);

        public T Value { get; private set; }
        public string ParserName { get; private set; }
    }
}
