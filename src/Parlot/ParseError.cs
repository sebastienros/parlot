namespace Parlot;

public class ParseError
{
    public string? Message { get; set; }
    public TextPosition Position { get; set; }

    /// <summary>
    /// Whether the parse failed because the input is nested too deeply, see <see cref="ParseException.IsNestedTooDeeply"/>.
    /// </summary>
    public bool IsNestedTooDeeply { get; set; }

    /// <summary>
    /// Why the parse failed, when a parser which rejected what it read there recorded a reason, see
    /// <see cref="Cursor.FurthestReason"/>: <see cref="Position"/> is then the furthest failure, and <see cref="Message"/>
    /// the reason when it is a <see langword="string"/>, its text otherwise. <see langword="null"/> for the other errors.
    /// </summary>
    public object? Reason { get; set; }

    // The error of a parse which failed without an exception: the reason recorded at the furthest failure, if any. None
    // when diagnostics are collected: an error is then a fatal one, the errors of the grammar are in the diagnostics.
    internal static ParseError? FromFurthestReason(Fluent.ParseContext context)
    {
        if (context.CollectDiagnostics)
        {
            return null;
        }

        var cursor = context.Scanner.Cursor;
        var reason = cursor.FurthestReason;

        if (reason is null)
        {
            return null;
        }

        return new ParseError
        {
            Message = reason as string ?? reason.ToString() ?? "",
            Position = cursor.FurthestPosition,
            Reason = reason
        };
    }
}
