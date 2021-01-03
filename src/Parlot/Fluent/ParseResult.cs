using System;

namespace Parlot
{
    public struct ParseResult<T>
    {
        public ParseResult(string buffer, in TextPosition start, in TextPosition end, string parserName, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Value = value;
            ParserName = parserName;
        }

        public void Set(string buffer, in TextPosition start, in TextPosition end, string parserName, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Value = value;
            ParserName = parserName;
        }

        public TextPosition Start;
        public TextPosition End;
        public string Buffer;
        public T Value;
        public string ParserName;
    }
    
    public static class ParseResultExtensions
    {
        public static ReadOnlySpan<char> GetSpan(this ParseResult<T> result) => result.Buffer.AsSpan(result.Start.Offset, result.Length);
    }
}
