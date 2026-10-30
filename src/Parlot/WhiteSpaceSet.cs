namespace Parlot;

/// <summary>
/// The chars that are white space when a parse skips or reads it, see <see cref="Fluent.ParseContext.WhiteSpaceSet"/>.
/// </summary>
/// <remarks>
/// Each set is a bit in the char table Parlot already reads, so selecting one keeps the white space checks to a single
/// lookup per char. In each set <c>\n</c>, <c>\r</c> and <c>\v</c> are the new lines (<see cref="Character.NewLines"/>).
/// Use <see cref="Fluent.ParseContext.WhiteSpaceParser"/> for any other set, or to skip comments.
/// </remarks>
public enum WhiteSpaceSet : byte
{
    /// <summary>
    /// The ASCII blanks, the no-break space, the Unicode spaces and the new lines: <see cref="Character.WhiteSpaceOrNewLines"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The six ASCII white space chars only: space, <c>\t</c>, <c>\f</c> and the new lines, <see cref="Character.AsciiWhiteSpaceOrNewLines"/>.
    /// These are the chars of C's <c>isspace</c> and Ruby's <c>\s</c>.
    /// </summary>
    Ascii = 1,
}
