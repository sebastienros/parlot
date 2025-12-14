using System;
using System.Globalization;

namespace Parlot;

public readonly struct TextSpan : IEquatable<string>, IEquatable<TextSpan>
{
    public static readonly TextSpan Empty = new(string.Empty);
    
    public TextSpan(string? value)
    {
        Buffer = value;
        Offset = 0;
        Length = value == null ? 0 : value.Length;
    }

    public TextSpan(string? buffer, int offset, int count)
    {
        Buffer = buffer;
        Offset = offset;
        Length = count;
    }

    public readonly int Length;
    public readonly int Offset;
    public readonly string? Buffer;

    public ReadOnlySpan<char> Span => Buffer == null ? [] : Buffer.AsSpan(Offset, Length);

    public override string ToString()
    {
        return Buffer?.Substring(Offset, Length) ?? "";
    }

    public bool Equals(string? other)
    {
        if (other == null)
        {
            return Buffer == null;
        }

        return Span.SequenceEqual(other.AsSpan());
    }

    public bool Equals(TextSpan other)
    {
        return Span.SequenceEqual(other.Span);
    }

    public static implicit operator TextSpan(string s)
    {
        return new TextSpan(s);
    }

    public override bool Equals(object? obj)
    {
        return obj is TextSpan t && Equals(t);
    }

    public override int GetHashCode()
    {
#if NET6_0_OR_GREATER
        return CultureInfo.InvariantCulture.CompareInfo.GetHashCode(Span, CompareOptions.Ordinal);
#else
        return (ToString() ?? "").GetHashCode();
#endif
    }

    public static bool operator ==(TextSpan left, TextSpan right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TextSpan left, TextSpan right)
    {
        return !(left == right);
    }
}
