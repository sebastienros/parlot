using System;

namespace Parlot;

public class ParseException : Exception
{
    public ParseException(string message, TextPosition position) : base(message)
    {
        Position = position;
    }

    public TextPosition Position { get; set; }

    /// <summary>
    /// Whether the parse failed because the input is nested too deeply: for the recursion limit of the context, or for the
    /// stack of the thread.
    /// </summary>
    public bool IsNestedTooDeeply { get; internal set; }
}
