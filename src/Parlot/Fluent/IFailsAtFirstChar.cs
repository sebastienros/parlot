namespace Parlot.Fluent;

/// <summary>
/// A parser which, where it doesn't match, fails at its first chars without invoking another parser than a literal: a
/// literal, which only reads the text (a text, a char, a keyword, an identifier, a number, a set of chars...), or a parser
/// which only wraps one, like <see cref="SkipWhiteSpace{T}"/>, <see cref="NoNewLineBefore{T}"/> or <see cref="Then{T, U}"/>.
/// </summary>
/// <remarks>
/// Trying it where it doesn't match costs about what reading the char after the white space does, so a choice of a few of
/// them which invokes them before the white space tries them in order rather than with a lookup table (#581). A
/// <see cref="Deferred{T}"/> doesn't forward it: it counts the recursion, and can be set to a parser holding itself. It is read once,
/// where the choice resolves how it skips the white space, from the parsers' fields: no reflection, which native AOT trims.
/// </remarks>
internal interface IFailsAtFirstChar
{
    bool FailsAtFirstChar { get; }
}

internal static class FailsAtFirstChar
{
    /// <summary>
    /// Whether <paramref name="parser"/> fails at its first chars without invoking another parser than a literal.
    /// </summary>
    public static bool Of(object parser) => parser is IFailsAtFirstChar { FailsAtFirstChar: true };
}
