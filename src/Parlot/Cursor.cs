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

    public TextPosition Position => new(Offset, _line, _column);

    /// <summary>
    /// Returns the <see cref="ReadOnlySpan{T}"/> value of the <see cref="Buffer" /> at the current offset.
    /// </summary>
    public ReadOnlySpan<char> Span => Buffer.AsSpan(Offset);

    /// <summary>
    /// Advances the cursor by one character.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance()
    {
        Offset++;

        if (Offset >= _textLength)
        {
            Eof = true;
            _column++;
            Current = NullChar;
            return;
        }

        var next = Buffer[Offset];

        if (Current == '\n')
        {
            _line++;
            _column = 1;
        }
        else if (next != '\r')
        {
            _column++;
        }

        // if c == '\r', don't increase the column count

        Current = next;
    }

    /// <summary>
    /// Advances the cursor.
    /// </summary>
    public void Advance(int count)
    {
        if (Eof)
        {
            return;
        }

        var maxOffset = Offset + count;

        // Detect if the cursor will be over Eof
        if (maxOffset > _textLength - 1)
        {
            Eof = true;
            maxOffset = _textLength - 1;
        }

        while (Offset < maxOffset)
        {
            Offset++;

            var next = Buffer[Offset];

            if (Current == '\n')
            {
                _line++;
                _column = 1;
            }
            else if (next != '\r')
            {
                _column++;
            }

            // if c == '\r', don't increase the column count

            Current = next;
        }

        if (Eof)
        {
            Current = NullChar;
            Offset = _textLength;
            _column += 1;
        }
    }

    /// <summary>
    /// Advances the cursor with the knowledge there are no new lines.
    /// </summary>
    public void AdvanceNoNewLines(int offset)
    {
        var newOffset = Offset + offset;
        var length = _textLength - 1;

        // Detect if the cursor will be over Eof
        if (newOffset > length)
        {
            Eof = true;
            _column += newOffset - length;
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
        // Ordinal comparison
        return Current == c;
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
