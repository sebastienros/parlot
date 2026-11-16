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
    /// started, contain a new line, or when the skip started at the start of the input (offset 0 at line 1 and column 1: not
    /// where a later value or delimited text of <c>ParseManyAsync</c> starts). Elsewhere its start is not a comment, and the
    /// comments added after it can start there. This is JavaScript's <c>--&gt;</c> in scripts (ECMAScript Annex B.1.1).
    /// </summary>
    LineStart,
}
