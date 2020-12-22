using System;

namespace Parlot
{
    public class ParseException : Exception
    {
        public ParseException(string message, TextPosition position) : base(message)
        {
            Position = position;
        }

        public TextPosition Position { get; set; }
    }
}
