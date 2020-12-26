using System;

namespace Parlot
{
    public interface IParseResult
    {
        /// <summary>
        /// Whether a token was successfully found.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// The start of the token.
        /// </summary>
        TextPosition Start { get; }

        /// <summary>
        /// The end of the token.
        /// </summary>
        TextPosition End { get; }

        /// <summary>
        /// The length of the token.
        /// </summary>
        int Length { get; }

        string Buffer { get; }

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
        void Succeed(string buffer, TextPosition start, TextPosition end, object Value);

        /// <summary>
        /// Initializes the token result.
        /// </summary>
        void Fail();

        object GetValue();
        void SetValue(object value);
    }

    public interface IParseResult<T> : IParseResult
    {
        void Succeed(string buffer, TextPosition start, TextPosition end, T Value);
        new T GetValue();
        void SetValue(T value);
    }
}
