namespace Parlot.Rewriting;

/// <summary>
/// An <see cref="ISeekable"/> parser that can also tell the texts the input starts with when it matches, not only their
/// first chars. A choice uses them to only invoke the alternatives whose text is at the position.
/// </summary>
/// <remarks>
/// Like <see cref="ISeekable.ExpectedChars"/>, the prefixes are only a condition for the parser to match: the parser still
/// reads the text itself. A shorter prefix is still a valid one, so a parser which can't tell every char of a text gives
/// the first ones. They apply after the white space the parser skips, see <see cref="ISeekable.SkipWhitespace"/>.
/// </remarks>
internal interface ISeekablePrefixes : ISeekable
{
    /// <summary>
    /// Gets the texts one of which the input starts with when the parser matches, or <see langword="null"/> when these
    /// are only the <see cref="ISeekable.ExpectedChars"/>. None is empty.
    /// </summary>
    string[]? ExpectedPrefixes { get; }
}
