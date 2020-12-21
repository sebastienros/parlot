using System;

namespace Parlot
{

    // TODO: It might be necessary to expose the direct string and cache it locally to prevent
    // StringSegment from copying the value every time it needs to be accessed.

    // TODO: Understand how the inner value behind the token (integer, literal) could be materialized once and kept with the token.
    // It might also be the responsibility of the AST to hold such value (preferred solution).
    public struct Token
    {
        public static readonly Token Empty = new("", "", TextPosition.Start, 0);

        public Token(string type, string buffer, TextPosition position, int length)
        {
            Buffer = buffer;
            Type = type;
            Position = position;
            Length = length;
        }

        public int Length { get; }
        public string Buffer { get; }
        public TextPosition Position { get; }
        public string Type { get; }
        public ReadOnlySpan<char> Span => Buffer.AsSpan(Position.Offset, Length);
    }
}
