namespace Parlot.Fluent;

/// <summary>
/// A parser that can match without building its value, which the parsers that only need the text it matches, like
/// <see cref="Capture{T}"/>, invoke instead of <see cref="Parser{T}.Parse(ParseContext, ref ParseResult{T})"/>.
/// </summary>
/// <remarks>
/// It is for the parsers whose value allocates, like the lists of <see cref="ZeroOrMany{T}"/> and <see cref="OneOrMany{T}"/>.
/// The generated parsers do the same with <c>SourceGenerationContext.DiscardResult</c>.
/// </remarks>
internal interface IDiscardableParser
{
    /// <summary>
    /// Matches what <see cref="Parser{T}.Parse(ParseContext, ref ParseResult{T})"/> matches, moving the cursor, invoking the
    /// same parsers and calling the same <see cref="ParseContext"/> methods, without building the value.
    /// </summary>
    bool ParseWithoutValue(ParseContext context);
}
