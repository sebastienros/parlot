namespace Parlot.Fluent;

/// <summary>
/// Covariant parser interface that allows a parser of a derived type to be used
/// where a parser of a base type is expected.
/// </summary>
/// <typeparam name="T">The type of value this parser produces.</typeparam>
public interface IParser<out T>
{
    /// <summary>
    /// Attempts to parse the input and returns whether the parse was successful.
    /// </summary>
    /// <param name="context">The parsing context.</param>
    /// <param name="start">The start position of the parsed value.</param>
    /// <param name="end">The end position of the parsed value.</param>
    /// <param name="value">The parsed value if successful, as object to support covariance.</param>
    /// <returns>True if parsing was successful, false otherwise.</returns>
    bool Parse(ParseContext context, out int start, out int end, out object? value);
}
