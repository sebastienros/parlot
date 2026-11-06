using Parlot.Fluent;
using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Parlot;

public static partial class Character
{
    private const IdentifierOptions IdentifierEscapes = IdentifierOptions.UnicodeEscapes | IdentifierOptions.CodePointEscapes;

    /// <summary>
    /// Returns the length of the identifier at the start of <paramref name="span"/>, or 0 when there is none. This method
    /// supports the parsers built by <c>Identifier(Func&lt;int, bool&gt;, Func&lt;int, bool&gt;, IdentifierOptions)</c>
    /// and their generated code, and isn't meant to be invoked directly.
    /// </summary>
    /// <param name="span">The text, from the position of the identifier.</param>
    /// <param name="startLow">The ASCII chars below 64 <paramref name="isStart"/> accepts, as bits.</param>
    /// <param name="startHigh">The ASCII chars from 64 <paramref name="isStart"/> accepts, as bits.</param>
    /// <param name="partLow">The ASCII chars below 64 <paramref name="isPart"/> accepts, as bits.</param>
    /// <param name="partHigh">The ASCII chars from 64 <paramref name="isPart"/> accepts, as bits.</param>
    /// <param name="isStart">Whether a code point can start the identifier, asked for the chars the masks don't cover.</param>
    /// <param name="isPart">Whether a code point can follow the start, asked for the chars the masks don't cover.</param>
    /// <param name="options">The escape sequences and surrogate pairs which are code points.</param>
    /// <param name="hitEnd">Whether the length depends on what follows <paramref name="span"/>, because an escape sequence
    /// or a surrogate pair was cut by its end.</param>
    /// <returns>The length of the identifier.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static int ScanIdentifier(
        ReadOnlySpan<char> span,
        ulong startLow,
        ulong startHigh,
        ulong partLow,
        ulong partHigh,
        Func<int, bool> isStart,
        Func<int, bool> isPart,
        IdentifierOptions options,
        out bool hitEnd)
    {
        hitEnd = false;

        if (span.IsEmpty)
        {
            hitEnd = true;
            return 0;
        }

        var pos = IsInAsciiMask(span[0], startLow, startHigh)
            ? 1
            : ReadIdentifierChar(span, 0, startLow, startHigh, isStart, options, ref hitEnd);

        if (pos == 0)
        {
            return 0;
        }

        // The ASCII chars are tested with the masks, the others are read by the slow path: a backslash starting an
        // escape sequence, a surrogate pair, or a char the predicate is asked for
        while (pos < span.Length)
        {
            var c = span[pos];

            if (IsInAsciiMask(c, partLow, partHigh))
            {
                pos++;
                continue;
            }

            if (c < 128 && c != '\\')
            {
                break;
            }

            var length = ReadIdentifierChar(span, pos, partLow, partHigh, isPart, options, ref hitEnd);

            if (length == 0)
            {
                break;
            }

            pos += length;
        }

        return pos;
    }

    /// <summary>
    /// Returns the bits of the ASCII chars a predicate accepts, but new lines, and the backslash when it starts escape
    /// sequences.
    /// </summary>
    internal static (ulong Low, ulong High) GetIdentifierAsciiMask(Func<int, bool> predicate, IdentifierOptions options)
    {
        ulong low = 0, high = 0;

        for (var c = 0; c < 128; c++)
        {
            if (c == '\\' && (options & IdentifierEscapes) != 0)
            {
                continue;
            }

            if (predicate(c))
            {
                if (c is '\n' or '\r')
                {
                    throw new InvalidOperationException("Identifiers cannot contain new lines.");
                }

                if (c < 64)
                {
                    low |= 1UL << c;
                }
                else
                {
                    high |= 1UL << (c - 64);
                }
            }
        }

        return (low, high);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInAsciiMask(char c, ulong low, ulong high) => c < 128 && (((c < 64 ? low : high) >> c) & 1) != 0;

    // Reads the identifier char at index, which the masks don't accept, returning its length or 0 when it isn't one
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReadIdentifierChar(ReadOnlySpan<char> span, int index, ulong low, ulong high, Func<int, bool> predicate, IdentifierOptions options, ref bool hitEnd)
    {
        var c = span[index];

        if (c < 128)
        {
            return c == '\\' && (options & IdentifierEscapes) != 0
                ? ReadIdentifierEscape(span, index, predicate, options, ref hitEnd)
                : IsInAsciiMask(c, low, high) ? 1 : 0;
        }

        if ((options & IdentifierOptions.SurrogatePairs) != 0 && char.IsSurrogate(c))
        {
            // A lone surrogate is not a code point
            if (!char.IsHighSurrogate(c))
            {
                return 0;
            }

            if (index + 1 == span.Length)
            {
                // The low surrogate can follow the end of the text
                hitEnd = true;
                return 0;
            }

            var next = span[index + 1];

            return char.IsLowSurrogate(next) && predicate(char.ConvertToUtf32(c, next)) ? 2 : 0;
        }

        return predicate(c) ? 1 : 0;
    }

    // Reads the escape sequence at index, which is a backslash, returning its length or 0 when it isn't an identifier char
    private static int ReadIdentifierEscape(ReadOnlySpan<char> span, int index, Func<int, bool> predicate, IdentifierOptions options, ref bool hitEnd)
    {
        var length = ReadIdentifierEscape(span.Slice(index), options, out var codePoint);

        if (length < 0)
        {
            // The escape sequence was cut by the end of the text
            hitEnd = true;
            return 0;
        }

        // An escaped char is accepted like the char itself
        return length > 0 && predicate(codePoint) ? length : 0;
    }

    // Returns the length of the escape sequence at the start of the span, 0 when there is none, or -1 when the end of the
    // span cuts it
    private static int ReadIdentifierEscape(ReadOnlySpan<char> span, IdentifierOptions options, out int codePoint)
    {
        codePoint = 0;

        if (span.Length < 2)
        {
            return span.Length == 1 && span[0] == '\\' ? -1 : 0;
        }

        if (span[0] != '\\' || span[1] != 'u')
        {
            return 0;
        }

        if (span.Length == 2)
        {
            return -1;
        }

        var i = 2;

        if (span[2] == '{' && (options & IdentifierOptions.CodePointEscapes) != 0)
        {
            i++;

            var start = i;

            while (true)
            {
                if (i == span.Length)
                {
                    return -1;
                }

                var d = span[i];

                if (d == '}')
                {
                    break;
                }

                if (!IsHexDigit(d))
                {
                    return 0;
                }

                codePoint = codePoint * 16 + HexValue(d);

                if (codePoint > 0x10FFFF)
                {
                    return 0;
                }

                i++;
            }

            // At least one digit
            return i == start ? 0 : i + 1;
        }

        if ((options & IdentifierOptions.UnicodeEscapes) == 0)
        {
            return 0;
        }

        for (; i < 6; i++)
        {
            if (i == span.Length)
            {
                return -1;
            }

            var d = span[i];

            if (!IsHexDigit(d))
            {
                return 0;
            }

            codePoint = codePoint * 16 + HexValue(d);
        }

        return 6;
    }

    /// <summary>
    /// Returns the name of an identifier, decoding its <c>\uXXXX</c> and <c>\u{X...}</c> escape sequences, for the
    /// identifiers built with <see cref="IdentifierOptions.UnicodeEscapes"/> or
    /// <see cref="IdentifierOptions.CodePointEscapes"/>.
    /// </summary>
    /// <param name="identifier">The text of the identifier.</param>
    /// <returns><paramref name="identifier"/> when it has no escape sequences, otherwise the decoded name.</returns>
    public static TextSpan DecodeIdentifier(TextSpan identifier)
    {
        var span = identifier.Span;

        if (span.IndexOf('\\') < 0)
        {
            return identifier;
        }

        return new TextSpan(DecodeIdentifierInternal(span));
    }

    /// <summary>
    /// Returns the name of an identifier, decoding its <c>\uXXXX</c> and <c>\u{X...}</c> escape sequences, for the
    /// identifiers built with <see cref="IdentifierOptions.UnicodeEscapes"/> or
    /// <see cref="IdentifierOptions.CodePointEscapes"/>.
    /// </summary>
    /// <param name="identifier">The text of the identifier.</param>
    /// <returns><paramref name="identifier"/> when it has no escape sequences, otherwise the decoded name.</returns>
    public static string DecodeIdentifier(string identifier)
    {
        ThrowHelper.ThrowIfNull(identifier, nameof(identifier));

        if (identifier.IndexOf('\\') < 0)
        {
            return identifier;
        }

        return DecodeIdentifierInternal(identifier.AsSpan());
    }

    private static string DecodeIdentifierInternal(ReadOnlySpan<char> span)
    {
        // An escape sequence is longer than the one or two chars it decodes to
        char[]? rentedBuffer = null;
        Span<char> buffer = span.Length <= 128
            ? stackalloc char[span.Length]
            : (rentedBuffer = ArrayPool<char>.Shared.Rent(span.Length));

        try
        {
            var dataIndex = 0;
            var i = 0;

            while (i < span.Length)
            {
                var codePoint = 0;
                var length = span[i] == '\\' ? ReadIdentifierEscape(span.Slice(i), IdentifierEscapes, out codePoint) : 0;

                if (length <= 0)
                {
                    // Not an escape sequence, kept as is
                    buffer[dataIndex++] = span[i++];
                    continue;
                }

                if (codePoint < 0x10000)
                {
                    buffer[dataIndex++] = (char)codePoint;
                }
                else
                {
                    codePoint -= 0x10000;
                    buffer[dataIndex++] = (char)(0xD800 + (codePoint >> 10));
                    buffer[dataIndex++] = (char)(0xDC00 + (codePoint & 0x3FF));
                }

                i += length;
            }

            return buffer.Slice(0, dataIndex).ToString();
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }
}
