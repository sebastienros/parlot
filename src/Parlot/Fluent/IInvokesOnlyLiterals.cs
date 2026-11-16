namespace Parlot.Fluent;

/// <summary>
/// A parser which doesn't invoke another parser than literals: a literal, which only reads the text (a text, a char, a
/// keyword, an identifier, a number, a set of chars...), or a parser which only wraps one or a choice of them, like
/// <see cref="SkipWhiteSpace{T}"/>, <see cref="NoNewLineBefore{T}"/> or <see cref="Then{T, U}"/>. Most of them fail at their
/// first char where they don't match.
/// </summary>
/// <remarks>
/// Trying it where it doesn't match costs about what reading the char after the white space does, unlike a sequence, which
/// invokes what it starts with before it fails, so a choice of two of them which invokes them before the white space tries
/// them in order rather than with a lookup table (#581). A type which doesn't implement it only costs that choice its table
/// lookup. A <see cref="Deferred{T}"/> doesn't forward it: it counts the recursion, and can be set to a parser holding itself.
/// It is read where the choice resolves how it skips the white space, from the parsers' fields: no reflection, which native
/// AOT trims.
/// </remarks>
internal interface IInvokesOnlyLiterals
{
    bool InvokesOnlyLiterals { get; }
}

internal static class InvokesOnlyLiterals
{
    /// <summary>
    /// Whether <paramref name="parser"/> invokes no other parser than literals.
    /// </summary>
    public static bool Of(object parser) => parser is IInvokesOnlyLiterals { InvokesOnlyLiterals: true };
}
