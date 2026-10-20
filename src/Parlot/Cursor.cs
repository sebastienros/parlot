using System;
using System.Runtime.CompilerServices;

namespace Parlot;

public class Cursor
{
    public const char NullChar = '\0';

    private readonly int _textLength;
    private int _line;
    private int _column;

    public Cursor(string buffer, in TextPosition position)
    {
        Buffer = buffer;
        _textLength = Buffer.Length;
        Eof = _textLength == 0;
        Current = _textLength == 0 ? NullChar : Buffer[position.Offset];
        Offset = 0;
        _line = 1;
        _column = 1;
    }

    public Cursor(string buffer) : this(buffer, TextPosition.Start)
    {
    }

    /// <summary>
    /// The current position. The offset is absolute, the line starts at 1 and increases after each <c>'\n'</c>, and the
    /// column is one more than the number of chars between the previous <c>'\n'</c> (or the start of the text) and the
    /// position: every char other than <c>'\n'</c>, <c>'\r'</c> included, occupies one column.
    /// </summary>
    public TextPosition Position => new(Offset, _line, _column);

    /// <summary>
    /// Returns the <see cref="ReadOnlySpan{T}"/> value of the <see cref="Buffer" /> at the current offset.
    /// </summary>
    public ReadOnlySpan<char> Span => Buffer.AsSpan(Offset);

    /// <summary>
    /// Advances the cursor by one character. Nothing happens when the cursor is already at the end of the text.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance()
    {
        var previous = Current;

        Offset++;

        if (Offset >= _textLength)
        {
            // Stays at the end of the text when already there, otherwise leaves the last char
            Offset = _textLength;

            if (Eof)
            {
                return;
            }

            Eof = true;
            Current = NullChar;
        }
        else
        {
            Current = Buffer[Offset];
        }

        // The char that was left either ended its line or occupied a column
        if (previous == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
    }

    /// <summary>
    /// Advances the cursor by <paramref name="count"/> characters, or to the end of the text, and tracks its current location (line and column).
    /// </summary>
    public void Advance(int count)
    {
        if (Eof)
        {
            return;
        }

#if NET8_0_OR_GREATER
        // Keep vector setup off the common short-token path.
        if (count >= 64 && TryAdvanceWithoutNewLines(count))
        {
            return;
        }
#endif

        var end = Offset + count;

        // Detect if the cursor will be over Eof
        if (end >= _textLength)
        {
            Eof = true;
            end = _textLength;
        }

        // Keep the loop state in locals and publish them once, as in Sep's block parsers.
        var offset = Offset;
        var line = _line;
        var column = _column;
        var buffer = Buffer;

        // Each char that is left counts as a column, or starts a new line
        while (offset < end)
        {
            if (buffer[offset] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }

            offset++;
        }

        Offset = offset;
        _line = line;
        _column = column;
        Current = Eof ? NullChar : buffer[offset];
    }

#if NET8_0_OR_GREATER
    private bool TryAdvanceWithoutNewLines(int count)
    {
        var offset = Offset;
        var end = Math.Min(offset + count, _textLength);

        // Only the chars that are left matter, not the one the cursor moves to. Guard overflow too.
        if (end < offset || Buffer.AsSpan(offset, end - offset).Contains('\n'))
        {
            return false;
        }

        _column += end - offset;
        Offset = end;

        if (end == _textLength)
        {
            Eof = true;
            Current = NullChar;
        }
        else
        {
            Current = Buffer[end];
        }

        return true;
    }
#endif

    /// <summary>
    /// Advances the cursor and tracks its current location (line and column) with the knowledge that the
    /// <paramref name="offset"/> chars it leaves contain no <c>'\n'</c>.
    /// </summary>
    public void AdvanceNoNewLines(int offset)
    {
        var newOffset = Offset + offset;
        var length = _textLength - 1;

        // Detect if the cursor will be over Eof
        if (newOffset > length)
        {
            Eof = true;
            // Only the chars up to the end are counted, like Advance(int) does
            _column += _textLength - Offset;
            Offset = _textLength;
            Current = NullChar;
            return;
        }

        Current = Buffer[newOffset];
        Offset = newOffset;
        _column += offset;
    }

    /// <summary>
    /// Moves the cursor to the specific position
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetPosition(in TextPosition position)
    {
        if (position.Offset != Offset)
        {
            ResetPositionNotInlined(position);
        }
    }

    private void ResetPositionNotInlined(in TextPosition position)
    {
        Offset = position.Offset;
        _line = position.Line;
        _column = position.Column;

        // Eof might have been recorded
        if (Offset >= Buffer.Length)
        {
            Current = NullChar;
            Eof = true;
        }
        else
        {
            Current = Buffer[position.Offset];
            Eof = false;
        }
    }

    /// <summary>
    /// Evaluates the char at the current position.
    /// </summary>
    public char Current { get; private set; }

    /// <summary>
    /// Returns the cursor's position in the _buffer.
    /// </summary>
    public int Offset { get; private set; }

    /// <summary>
    /// Evaluates a char forward in the _buffer.
    /// </summary>
    public char PeekNext(int index = 1)
    {
        var nextIndex = Offset + index;

        if (nextIndex >= _textLength || nextIndex < 0)
        {
            return NullChar;
        }

        return Buffer[nextIndex];
    }

    public bool Eof { get; private set; }

    public string Buffer { get; }

    /// <summary>
    /// Whether a char is at the current position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Match(char c)
    {
        // Ordinal comparison. Current is also the null char at the end of the text, where there is no char to match.
        return Current == c && (c != NullChar || !Eof);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MatchAnyOf(ReadOnlySpan<char> s)
    {
        if (Eof)
        {
            return false;
        }

        return s.Length == 0 || s.IndexOf(Current) > -1;
    }

    /// <summary>
    /// Whether a string is at the current position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Match(ReadOnlySpan<char> s)
    {
        // Equivalent to StringComparison.Ordinal comparison

        if (_textLength < Offset + s.Length)
        {
            return false;
        }

        return Span.StartsWith(s);
    }

    /// <summary>
    /// Whether a string is at the current position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Match(ReadOnlySpan<char> s, StringComparison comparisonType)
    {
        if (_textLength < Offset + s.Length)
        {
            return false;
        }

        return Span.StartsWith(s, comparisonType);
    }
}
