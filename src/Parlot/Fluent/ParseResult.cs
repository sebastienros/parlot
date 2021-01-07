using System;

namespace Parlot
{
    public struct ParseResult<T>
    {
        public ParseResult(string buffer, int start, int end, string parserName, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Value = value;
            ParserName = parserName;
        }

        public void Set(string buffer, int start, int end, string parserName, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Value = value;
            ParserName = parserName;
        }

        public int Start;
        public int End;
        public string Buffer;
        public T Value;
        public string ParserName;
    }
}
