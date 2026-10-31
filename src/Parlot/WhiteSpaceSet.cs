namespace Parlot;

/// <summary>
/// The chars that are white space when a parse skips or reads it, see <see cref="Fluent.ParseContext.WhiteSpaceSet"/>.
/// </summary>
/// <remarks>
/// Each set is a bit in the char table Parlot already reads, so selecting one keeps the white space checks to a single
/// lookup per char. A set's line breaks are its new lines, which <see cref="Fluent.ParseContext.UseNewLines"/> keeps from being
/// skipped. Use <see cref="Fluent.ParseContext.WhiteSpaceParser"/> for any other set, or to skip comments.
/// </remarks>
public enum WhiteSpaceSet : byte
{
    /// <summary>
    /// The Unicode <c>White_Space</c> chars, the ones <see cref="char.IsWhiteSpace(char)"/> accepts: <see cref="Character.WhiteSpaceOrNewLines"/>.
    /// The new lines are <c>\n</c>, <c>\r</c>, <c>\v</c>, U+0085, U+2028 and U+2029 (<see cref="Character.NewLines"/>).
    /// </summary>
    Default = 0,

    /// <summary>
    /// The six ASCII white space chars only: space, <c>\t</c>, <c>\f</c> and the new lines <c>\n</c>, <c>\r</c> and <c>\v</c>
    /// (<see cref="Character.AsciiNewLines"/>), <see cref="Character.AsciiWhiteSpaceOrNewLines"/>.
    /// These are the chars of C's <c>isspace</c> and Ruby's <c>\s</c>.
    /// </summary>
    Ascii = 1,
}
