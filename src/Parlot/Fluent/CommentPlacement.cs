namespace Parlot.Fluent;

/// <summary>
/// Where <see cref="CommentsBuilder.WithSingleLine(string, string, CommentPlacement)"/> starts a comment.
/// </summary>
public enum CommentPlacement
{
    /// <summary>
    /// The comment starts anywhere.
    /// </summary>
    Anywhere,

    /// <summary>
    /// The comment only starts at the start of a line: when the white space and comments skipped before it, since the skip
    /// started, contain a new line, or when the skip started at the start of the input or after a new line, which a token
    /// read with <see cref="ParseContext.UseNewLines"/>, or <c>ParseManyAsync</c> skipped before a value or a delimited text.
    /// Elsewhere its start is not a comment, and the comments added after it can start there. This is JavaScript's <c>--&gt;</c> in scripts (ECMAScript Annex B.1.1).
    /// </summary>
    LineStart,
}
