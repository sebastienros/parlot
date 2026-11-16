namespace Parlot.Fluent;

/// <summary>
/// A parser which never moves the cursor when it succeeds, like <see cref="Not{T}"/> and <see cref="Peek{T}"/>: a sequence
/// which starts with it reads from where it is with both its parser and the parser that follows, and takes its
/// <see cref="Rewriting.ISeekable"/> hints from them: those of its parser for a <c>Peek</c>, of both for a <c>Not</c>, see
/// <see cref="SeekableHints.Sequence"/>.
/// </summary>
internal interface ILookahead
{
    /// <summary>
    /// Gets the parser it looks ahead with, invoked where the lookahead is.
    /// </summary>
    object Parser { get; }

    /// <summary>
    /// Gets whether the lookahead only succeeds where <see cref="Parser"/> matches, like <see cref="Peek{T}"/>, rather than
    /// where it doesn't, like <see cref="Not{T}"/>.
    /// </summary>
    bool MatchesParser { get; }
}
