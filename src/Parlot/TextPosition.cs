namespace Parlot
{
    /// <summary>
    /// Represents a position in a text buffer.
    /// </summary>
    public struct TextPosition
    {
        public static TextPosition Start = new(0, 0, 0);

        public TextPosition(int offset, int line, int column)
        {
            Offset = offset;
            Line = line;
            Column = column;
        }

        public TextPosition(TextPosition textPosition) : this(textPosition.Offset, textPosition.Line, textPosition.Column)
        {
        }

        public int Offset;
        public int Line;
        public int Column;

        public TextPosition NextColumn()
        {
            return new TextPosition(Offset + 1, Line, Column + 1);
        }

        public TextPosition NextLine()
        {
            return new TextPosition(Offset, Line + 1, 0);
        }

        public static int operator -(TextPosition left, TextPosition right)
        {
            return left.Offset - right.Offset;
        }
    }
}