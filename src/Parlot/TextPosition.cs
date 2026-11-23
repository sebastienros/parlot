using System;

namespace Parlot;

/// <summary>
/// Represents a position in a text buffer.
/// </summary>
public readonly struct TextPosition : IEquatable<TextPosition>
{
    public static readonly TextPosition Start = new(0, 1, 1);

    public TextPosition(int offset, int line, int column)
    {
        Offset = offset;
        Line = line;
        Column = column;
    }

    public readonly int Offset;
    public readonly int Line;
    public readonly int Column;

    public static int operator -(in TextPosition left, in TextPosition right)
    {
        return left.Offset - right.Offset;
    }

    /// <inheritdoc />
    public bool Equals(TextPosition other)
        => Offset == other.Offset && Line == other.Line && Column == other.Column;

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is TextPosition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Offset;
            hash = (hash * 397) ^ Line;
            hash = (hash * 397) ^ Column;
            return hash;
        }
    }

    /// <summary>
    /// Determines whether two <see cref="TextPosition"/> values are equal.
    /// </summary>
    public static bool operator ==(TextPosition left, TextPosition right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="TextPosition"/> values are not equal.
    /// </summary>
    public static bool operator !=(TextPosition left, TextPosition right) => !left.Equals(right);

    public override string ToString() => $"({Line}:{Column})";
}
