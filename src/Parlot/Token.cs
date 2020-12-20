using Microsoft.Extensions.Primitives;

namespace Parlot
{

    // TODO: It might be necessary to expose the direct string and cache it locally to prevent
    // StringSegment from copying the value every time it needs to be accessed.

    // TODO: Understand how the inner value behind the token (integer, literal) could be materialized once and kept with the token.
    // It might also be the responsibility of the AST to hold such value (preferred solution).
    public struct Token<T>
    {
        public static readonly Token<T> Empty = new Token<T>(default(T), null, TextPosition.Start, 0);

        public Token(T type, string buffer, TextPosition position, int length)
        {
            Type = type;
            Position = position;
            Segment = new StringSegment(buffer, position.Offset, length);
        }

        public TextPosition Position { get; }
        public T Type { get; }
        public StringSegment Segment { get; }
    }
}
