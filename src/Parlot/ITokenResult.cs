using System;

namespace Parlot
{
    public interface ITokenResult
    {
        /// <summary>
        /// Whether a token was successfully found.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// The start of the token.
        /// </summary>
        int Start { get; }

        /// <summary>
        /// The end of the token.
        /// </summary>
        int End { get; }

        /// <summary>
        /// The length of the token.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Returns the text associated with the token.
        /// </summary>
        /// <remarks>Prefer using <see cref="Span"/> as it is non-allocating.</remarks>
        string Text { get; }

        /// <summary>
        /// Returns the span associated with the token.
        /// </summary>
        ReadOnlySpan<char> Span { get; }

        /// <summary>
        /// Sets the result.
        /// </summary>
        ITokenResult Succeed(string buffer, int start, int end);

        /// <summary>
        /// Initializes the token result.
        /// </summary>
        ITokenResult Fail();
    }
}
