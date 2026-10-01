using System;
using System.Runtime.CompilerServices;

namespace Parlot;

public class Cursor
{
    public const char NullChar = '\0';

    private readonly int _textLength;
    private int _line;
    private int _column;

    // Set unconditionally on the cold end-of-buffer paths; only observable through HitEnd when the buffer is not final.
    private bool _hitEnd;

    /// <summary>
    /// Creates a cursor over <paramref name="buffer"/> starting at <paramref name="position"/>.
    /// </summary>
    /// <param name="buffer">The text to read.</param>
    /// <param name="position">The initial position. Its offset indexes into <paramref name="buffer"/>, its line and column are reported as-is.</param>
    public Cursor(string buffer, in TextPosition position) : this(buffer, position, isFinal: true)
    {
    }

    /// <summary>
    /// Creates a cursor over <paramref name="buffer"/> starting at <paramref name="position"/>.
    /// </summary>
    /// <param name="buffer">The text to read.</param>
    /// <param name="position">The initial position. Its offset indexes into <paramref name="buffer"/>, its line and column are reported as-is.</param>
    /// <param name="isFinal">
    /// <see langword="false"/> when more text may follow <paramref name="buffer"/>, as when parsing a stream.
    /// The cursor then records in <see cref="HitEnd"/> whether a decision depended on the end of the buffer.
    /// </param>
    public Cursor(string buffer, in TextPosition position, bool isFinal)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _textLength = Buffer.Length;

        if ((uint)position.Offset > (uint)_textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        Offset = position.Offset;
        _line = position.Line;
        _column = position.Column;
        IsFinal = isFinal;
        Eof = Offset == _textLength;
        _hitEnd = Eof;
        Current = Eof ? NullChar : Buffer[Offset];
    }

    /// <summary>
    /// Whether <see cref="Buffer"/> holds the end of the input. <see langword="true"/> unless the cursor was created
    /// for a window of a larger input, as when parsing a stream.
    /// </summary>
    public bool IsFinal { get; }

    /// <summary>
    /// Whether, since the cursor was created, a read reached or looked past the end of a non-final <see cref="Buffer"/>.
    /// A parse result obtained while this is <see langword="true"/> may change once more text is available.
    /// Always <see langword="false"/> when <see cref="IsFinal"/> is <see langword="true"/>.
    /// </summary>
    /// <remarks>The flag is sticky: moving the cursor back with <see cref="ResetPosition(in TextPosition)"/> does not clear it.</remarks>
    public bool HitEnd => _hitEnd && !IsFinal;

    /// <summary>
    /// Records that the current decision depended on text beyond the end of <see cref="Buffer"/>.
    /// Custom parsers that inspect <see cref="Span"/> or <see cref="Buffer"/> directly must call this when they stop,
    /// or fail, because the buffer ended rather than because of its content.
    /// Moving the cursor to the end, <see cref="PeekNext(int)"/> past it and the <c>Match</c> methods already do it.
    /// </summary>
    public void MarkHitEnd()
    {
        _hitEnd = true;
    }

    internal void ResetHitEnd()
    {
        _hitEnd = Eof;
    }

    /// <summary>
    /// Gets the remaining text when at least <paramref name="minLength"/> characters are available.
    /// Otherwise records <see cref="HitEnd"/> and returns <see langword="false"/>.
    /// </summary>
    public bool TryGetSpan(int minLength, out ReadOnlySpan<char> span)
    {
        span = Span;

        if (span.Length < minLength)
        {
            _hitEnd = true;
            return false;
        }

        return true;
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
            _hitEnd = true;
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
    /// Advances the cursor and tracks its current location (line and column).
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

        var maxOffset = Offset + count;

        // Detect if the cursor will be over Eof
        if (maxOffset > _textLength - 1)
        {
            Eof = true;
            _hitEnd = true;
            maxOffset = _textLength - 1;
        }

        // Keep the loop state in locals and publish it once, as in Sep's block parsers.
        var offset = Offset;
        var current = Current;
        var line = _line;
        var column = _column;
        var buffer = Buffer;

        while (offset < maxOffset)
        {
            var next = buffer[++offset];

            if (current == '\n')
            {
                line++;
                column = 1;
            }
            else if (next != '\r')
            {
                column++;
            }

            current = next;
        }

        Offset = offset;
        Current = current;
        _line = line;
        _column = column;

        if (Eof)
        {
            Current = NullChar;
            Offset = _textLength;
            _column++;
        }
    }

#if NET8_0_OR_GREATER
    private bool TryAdvanceWithoutNewLines(int count)
    {
        var offset = Offset;
        var end = Math.Min(offset + count, _textLength - 1);

        // Include both endpoints: LF affects the following position, while CR
        // affects the position at which it is encountered. Guard overflow too.
        if (end < offset || Buffer.AsSpan(offset, end - offset + 1).ContainsAny('\r', '\n'))
        {
            return false;
        }

        _column += end - offset;
        Offset = end;
        Current = Buffer[end];

        if (count > end - offset)
        {
            Eof = true;
            _hitEnd = true;
            Current = NullChar;
            Offset = _textLength;
            _column++;
        }

        return true;
    }
#endif

    /// <summary>
    /// Advances the cursor and tracks its current location (line and column) with the knowledge there are no new lines (\r or \n).
    /// </summary>
    public void AdvanceNoNewLines(int offset)
    {
        var newOffset = Offset + offset;
        var length = _textLength - 1;

        // Detect if the cursor will be over Eof
        if (newOffset > length)
        {
            Eof = true;
            _hitEnd = true;
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
            _hitEnd = true;
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
            if (nextIndex >= _textLength)
            {
                _hitEnd = true;
            }

            return NullChar;
        }

        return Buffer[nextIndex];
    }

    /// <summary>
    /// Advances the cursor by a precomputed amount, applying known line/column deltas.
    /// </summary>
    /// <param name="consumedLength">How many characters were consumed.</param>
    /// <param name="newLines">Number of newline characters encountered in the consumed span.</param>
    /// <param name="trailingSegmentLength">Length of the segment after the last newline (or the full length when there are no newlines).</param>
    public void AdvanceBy(int consumedLength, int newLines, int trailingSegmentLength)
    {
        Offset += consumedLength;

        if (Offset >= Buffer.Length)
        {
            Eof = true;
            _hitEnd = true;
            Offset = Buffer.Length;
            Current = NullChar;
        }
        else
        {
            Eof = false;
            Current = Buffer[Offset];
        }

        if (newLines == 0)
        {
            _column += consumedLength;
        }
        else
        {
            _line += newLines;
            _column = 1 + trailingSegmentLength;
        }
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
            MarkHitEndIfPrefix(s, StringComparison.Ordinal);
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
            MarkHitEndIfPrefix(s, comparisonType);
            return false;
        }

        return Span.StartsWith(s, comparisonType);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void MarkHitEndIfPrefix(ReadOnlySpan<char> s, StringComparison comparisonType)
    {
        if (IsFinal)
        {
            return;
        }

        // A shorter remainder only proves a mismatch when it already differs from the pattern.
        // Culture-sensitive comparisons are not prefix-stable, so they always depend on more text.
        if ((comparisonType != StringComparison.Ordinal && comparisonType != StringComparison.OrdinalIgnoreCase)
            || s.StartsWith(Span, comparisonType))
        {
            _hitEnd = true;
        }
    }
}
