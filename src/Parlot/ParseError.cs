namespace Parlot;

public class ParseError
{
    public string? Message { get; set; }
    public TextPosition Position { get; set; }

    /// <summary>
    /// Whether the parse failed because the input is nested too deeply, see <see cref="ParseException.IsNestedTooDeeply"/>.
    /// </summary>
    public bool IsNestedTooDeeply { get; set; }
}
