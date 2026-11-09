namespace Parlot.Fluent;

/// <summary>
/// What <see cref="CommentsBuilder.WithMultiLine(string, string, UnterminatedComment)"/> does with a multi line comment
/// whose end is not in the text.
/// </summary>
public enum UnterminatedComment
{
    /// <summary>
    /// Throws a <see cref="ParseException"/> after the start of the comment, or reports an error when the diagnostics are
    /// collected, in which case the comment is not skipped.
    /// </summary>
    Throw,

    /// <summary>
    /// The start of the comment is not a comment: it is not skipped, and the cursor stays before it. The comments added
    /// after it can still start there.
    /// </summary>
    NotAComment,

    /// <summary>
    /// The comment runs to the end of the text.
    /// </summary>
    ToEnd,
}
