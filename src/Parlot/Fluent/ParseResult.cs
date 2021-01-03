namespace Parlot
{
    public struct ParseResult<T>
    {
        public ParseResult(string buffer, in TextPosition start, in TextPosition end, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Value = value;
        }

        public void Set(string buffer, in TextPosition start, in TextPosition end, T value)
        {
            Buffer = buffer;
            Start = start;
            End = end;
            Value = value;
        }

        public TextPosition Start;
        public TextPosition End;
        public string Buffer;
        public T Value;
    }
    
    // if really needed, allows less fields if can be computed, no need to methods
    internal static class ParseResultExtensions
    {
        public static int GetLength<T>(this ParseResult<T> result) => result.End - result.Start;
    }
}
