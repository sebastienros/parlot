namespace Parlot
{
    /// <summary>
    /// Represents a position in a text buffer.
    /// </summary>
    public struct TextPosition
    {
        public static TextPosition Start = new(0, 1, 1);

        public TextPosition(int offset, int line, int column)
        {
            Offset = offset;
            Line = line;
            Column = column;
        }

        public int Offset { get; }
        public int Line { get; }
        public int Column { get; }

        public static int operator -(TextPosition left, TextPosition right)
        {
            return left.Offset - right.Offset;
        }

        public override string ToString() => $"({Line}:{Column})";
    }
}
