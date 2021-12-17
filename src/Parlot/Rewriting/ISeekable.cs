namespace Parlot.Rewriting
{
    /// <summary>
    /// A Parser implementing this interface can only be triggered if the next char matches the one provided.
    /// It is used to create char lookups to optimize which Parsers need to be invoked next.
    /// </summary>
    public interface ISeekable
    {
        /// <summary>
        /// Gets whether the current parser can be selected from a single char.
        /// This could vary based on the subsequent parsers.
        /// </summary>
        bool CanSeek { get; }

        /// <summary>
        /// Gets the char that should be matched next to evaluate this Parser.
        /// </summary>
        char ExpectedChar { get; }

        /// <summary>
        /// Gets whether the current parser needs to skip whitespaces before being invoked.
        /// </summary>
        bool SkipWhitespace { get; }
    }
}
