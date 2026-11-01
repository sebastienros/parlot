using System;
using System.Runtime.CompilerServices;

namespace Parlot;

public class Cursor
{
    public const char NullChar = '\0';

    private int _textLength;

    // Index of the current char in Buffer. Offset adds _base, the absolute offset of Buffer[0], which is 0 unless
    // the cursor reads a compacting window of a stream (see ReplaceBuffer).
    private int _offset;
#if PARLOT_STRING_ONLY
    // Generated parsers without a TextReader entry point only parse strings: a constant base keeps the offset arithmetic out of their hot paths.
    private const int _base = 0;
#else
    private int _base;
#endif

    // The line and column are only computed when a position is requested, from the text (see GetPosition), such that moving
    // the cursor only updates the offset. Absolute offsets. The origin is the position the cursor was created at, the only
    // one whose line and column are given rather than counted. In compacting mode it moves to the start of the window when
    // the text before it is dropped. The last position computed is memoized such that requests for successive positions
    // only scan the text between them. Invariant: _base <= _originOffset <= _lastOffset <= _base + _textLength.
    private int _originOffset;
    private int _originLine;
    private int _originColumn;
    private int _lastOffset;
    private int _lastLine;
    private int _lastColumn;

    // The furthest absolute offset the cursor was moved back from, see FurthestOffset
    private int _furthestOffset;

    // HitEndFlag is set unconditionally on the cold end-of-buffer paths; only observable through HitEnd when the buffer is not final.
    // IsFinal shares the byte so that the cursor, which is allocated for each parse, doesn't grow.
    private byte _flags;
    private const byte HitEndFlag = 1;
    private const byte NotFinalFlag = 2;

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

        _offset = position.Offset;
        _originOffset = _lastOffset = _furthestOffset = position.Offset;
        _originLine = _lastLine = position.Line;
        _originColumn = _lastColumn = position.Column;
        Eof = _offset == _textLength;
        _flags = (byte)((isFinal ? 0 : NotFinalFlag) | (Eof ? HitEndFlag : 0));
        Current = Eof ? NullChar : Buffer[_offset];
    }

    /// <summary>
    /// Whether <see cref="Buffer"/> holds the end of the input. <see langword="true"/> unless the cursor was created
    /// for a window of a larger input, as when parsing a stream.
    /// </summary>
    public bool IsFinal => (_flags & NotFinalFlag) == 0;

    /// <summary>
    /// Whether, since the cursor was created, a read reached or looked past the end of a non-final <see cref="Buffer"/>.
    /// A parse result obtained while this is <see langword="true"/> may change once more text is available.
    /// Always <see langword="false"/> when <see cref="IsFinal"/> is <see langword="true"/>.
    /// </summary>
    /// <remarks>The flag is sticky: moving the cursor back with <see cref="ResetPosition(int)"/> does not clear it.</remarks>
    public bool HitEnd => _flags == (HitEndFlag | NotFinalFlag);

    /// <summary>
    /// Records that the current decision depended on text beyond the end of <see cref="Buffer"/>.
    /// Custom parsers that inspect <see cref="Span"/> or <see cref="Buffer"/> directly must call this when they stop,
    /// or fail, because the buffer ended rather than because of its content.
    /// Moving the cursor to the end, <see cref="PeekNext(int)"/> past it and the <c>Match</c> methods already do it.
    /// </summary>
    public void MarkHitEnd()
    {
        _flags |= HitEndFlag;
    }

    internal void ResetHitEnd()
    {
        _flags = (byte)((_flags & NotFinalFlag) | (Eof ? HitEndFlag : 0));
    }

    public Cursor(string buffer) : this(buffer, TextPosition.Start)
    {
    }

    /// <summary>
    /// The current position. The offset is absolute, the line starts at 1 and increases after each <c>'\n'</c>, and the
    /// column is one more than the number of chars between the previous <c>'\n'</c> (or the start of the text) and the
    /// position: every char other than <c>'\n'</c>, <c>'\r'</c> included, occupies one column.
    /// </summary>
    /// <remarks>
    /// The line and the column are computed when this property is read, by counting the <c>'\n'</c> chars between the
    /// last position read and this one, rather than tracked as the cursor moves. Reading it thus costs a scan of the text
    /// the cursor moved over since the last read. Parsers which save a position to move the cursor back to it should save
    /// <see cref="Offset"/> and call <see cref="ResetPosition(int)"/> instead.
    /// </remarks>
    public TextPosition Position => GetPosition(_offset + _base);

    /// <summary>
    /// Returns the position of an absolute offset of the buffered text, see <see cref="Position"/>.
    /// </summary>
    /// <remarks>
    /// The lines and columns of the offsets before the initial position of the cursor are counted backward from it, the
    /// start of the buffer starting a line. In compacting mode, the text discarded from the buffer can't be counted: an
    /// offset in it gets the line of the first buffered char, and its column assuming that no line ends between them.
    /// </remarks>
    internal TextPosition GetPosition(int offset)
    {
        if (offset != _lastOffset)
        {
            if (offset < _originOffset || offset - _base > _textLength)
            {
                return GetPositionOutsideOrigin(offset);
            }

            ComputePosition(offset, out var line, out var column);

            _lastOffset = offset;
            _lastLine = line;
            _lastColumn = column;
        }

        return new TextPosition(offset, _lastLine, _lastColumn);
    }

    // Computes the line and column of an offset between the origin and the end of the buffer
    private void ComputePosition(int offset, out int line, out int column)
    {
        if (offset >= _lastOffset)
        {
            CountForward(_lastOffset, _lastLine, _lastColumn, offset, out line, out column);
        }
        else if (offset - _originOffset <= _lastOffset - offset)
        {
            CountForward(_originOffset, _originLine, _originColumn, offset, out line, out column);
        }
        else
        {
            // Closer to the last position than to the origin: only the text between them, and the start of the line, are read
            var newLines = CountNewLines(GetSpan(offset, _lastOffset - offset));

            line = _lastLine - newLines;

            if (newLines == 0)
            {
                column = _lastColumn - (_lastOffset - offset);
            }
            else
            {
                var before = GetSpan(_originOffset, offset - _originOffset);
                var lastNewLine = before.LastIndexOf('\n');

                column = lastNewLine >= 0 ? before.Length - lastNewLine : _originColumn + before.Length;
            }
        }
    }

    // Computes the line and column of an offset from a known position before it
    private void CountForward(int fromOffset, int fromLine, int fromColumn, int offset, out int line, out int column)
    {
        var text = GetSpan(fromOffset, offset - fromOffset);
        var lastNewLine = text.LastIndexOf('\n');

        if (lastNewLine < 0)
        {
            line = fromLine;
            column = fromColumn + text.Length;
        }
        else
        {
            line = fromLine + CountNewLines(text.Slice(0, lastNewLine)) + 1;
            column = text.Length - lastNewLine;
        }
    }

    // The offsets before the origin, or after the end of the buffer, are not memoized such that the memoized position is
    // always one the text can be counted from
    private TextPosition GetPositionOutsideOrigin(int offset)
    {
        var end = _base + _textLength;

        if (offset > end)
        {
            // Only after ResetPosition moved the cursor past the end of the text: the extra chars are counted as columns
            var last = GetPosition(end);
            return new TextPosition(offset, last.Line, last.Column + (offset - end));
        }

        if (offset < _base)
        {
            // The text was discarded by a compacting stream, the origin is at the start of the buffer
            return new TextPosition(offset, _originLine, Math.Max(1, _originColumn - (_originOffset - offset)));
        }

        var line = _originLine;
        var column = _originColumn;
        var newLines = CountNewLines(GetSpan(offset, _originOffset - offset));

        if (newLines == 0)
        {
            column = Math.Max(1, _originColumn - (_originOffset - offset));
        }
        else
        {
            var before = GetSpan(_base, offset - _base);

            line = _originLine - newLines;
            column = before.Length - before.LastIndexOf('\n');
        }

        return new TextPosition(offset, line, column);
    }

    private static int CountNewLines(ReadOnlySpan<char> text)
    {
#if NET8_0_OR_GREATER
        return text.Count('\n');
#else
        var count = 0;
        int index;

        while ((index = text.IndexOf('\n')) >= 0)
        {
            count++;
            text = text.Slice(index + 1);
        }

        return count;
#endif
    }

    /// <summary>
    /// Returns the <see cref="ReadOnlySpan{T}"/> value of the <see cref="Buffer" /> at the current offset.
    /// </summary>
    public ReadOnlySpan<char> Span => Buffer.AsSpan(_offset);

    // The absolute offset of the first char of Buffer, 0 unless the input is streamed in compacting mode
    internal int BufferStart => _base;

    /// <summary>
    /// Returns the buffered text between the absolute offset <paramref name="start"/> and <c>start + length</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> GetSpan(int start, int length) => Buffer.AsSpan(start - _base, length);

    /// <summary>
    /// Creates a <see cref="TextSpan"/> over the buffered text between the absolute offset <paramref name="start"/> and <c>start + length</c>.
    /// </summary>
    /// <remarks>
    /// The <see cref="TextSpan.Offset"/> of the result is relative to its <see cref="TextSpan.Buffer"/>, which is the
    /// current <see cref="Buffer"/>. Parsers must create their <see cref="TextSpan"/> results with this method rather than
    /// from <see cref="Buffer"/> and an absolute offset, which are only equivalent when the input isn't streamed in compacting mode.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextSpan CreateSpan(int start, int length) => new(Buffer, start - _base, length);

    /// <summary>
    /// Whether the current position was discarded from the buffer, which only happens after a parser moved the cursor
    /// back to text it didn't retain in compacting mode.
    /// </summary>
    internal bool IsDiscarded => _offset < 0;

    /// <summary>
    /// The number of chars buffered after the current position.
    /// </summary>
    internal int Remaining => _textLength - _offset;

#if !PARLOT_STRING_ONLY
    /// <summary>
    /// Replaces <see cref="Buffer"/> with a window of the same input, keeping the absolute position.
    /// </summary>
    /// <param name="buffer">The new window.</param>
    /// <param name="bufferStart">The absolute offset of <paramref name="buffer"/>'s first char. It must not be past the current position.</param>
    /// <param name="isFinal">Whether <paramref name="buffer"/> ends with the end of the input.</param>
    internal void ReplaceBuffer(string buffer, int bufferStart, bool isFinal)
    {
        var offset = _offset + _base;

        // The lines are counted from the origin, the text before the new window is dropped: its lines are counted first
        if (bufferStart > _originOffset)
        {
            var start = GetPosition(bufferStart);

            _originOffset = _lastOffset = bufferStart;
            _originLine = _lastLine = start.Line;
            _originColumn = _lastColumn = start.Column;
        }

        Buffer = buffer;
        _textLength = buffer.Length;
        _base = bufferStart;
        _offset = offset - bufferStart;
        _flags = (byte)((_flags & HitEndFlag) | (isFinal ? 0 : NotFinalFlag));

        if (_offset >= _textLength)
        {
            _offset = _textLength;
            Eof = true;
            Current = NullChar;
        }
        else
        {
            Eof = false;
            Current = Buffer[_offset];
        }
    }
#endif

    /// <summary>
    /// Advances the cursor by one character. Nothing happens when the cursor is already at the end of the text.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance()
    {
        var offset = _offset + 1;

        if (offset < _textLength)
        {
            _offset = offset;
            Current = Buffer[offset];
        }
        else
        {
            AdvanceToEnd();
        }
    }

    /// <summary>
    /// Advances the cursor by <paramref name="count"/> characters, or to the end of the text.
    /// Nothing happens when the cursor is already at the end of the text, or when <paramref name="count"/> is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        if (Eof)
        {
            return;
        }

        // A single unsigned compare selects a count which doesn't reach the end of the text, and isn't negative
        if (unchecked((uint)count < (uint)(_textLength - _offset)))
        {
            _offset += count;
            Current = Buffer[_offset];
        }
        else if (count > 0)
        {
            AdvanceToEnd();
        }
    }

    /// <summary>
    /// Advances the cursor by <paramref name="offset"/> characters, or to the end of the text.
    /// </summary>
    /// <remarks>
    /// Lines and columns are computed from the text when <see cref="Position"/> is read, so the chars the cursor leaves can
    /// contain <c>'\n'</c> too: this is <see cref="Advance(int)"/>, which it predates, except that it doesn't check that
    /// <paramref name="offset"/> isn't negative.
    /// </remarks>
    public void AdvanceNoNewLines(int offset)
    {
        var newOffset = _offset + offset;

        if (newOffset >= _textLength)
        {
            AdvanceToEnd();
            return;
        }

        Current = Buffer[newOffset];
        _offset = newOffset;
    }

    /// <summary>
    /// Advances the cursor by <paramref name="count"/> chars which are in the buffer, up to its end included.
    /// </summary>
    /// <remarks>
    /// This is <see cref="AdvanceNoNewLines(int)"/> inlined: generated parsers which read the text through a span call it
    /// once the text matched, with the number of chars they read.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AdvanceWithinBuffer(int count)
    {
        var offset = _offset + count;
        var buffer = Buffer;

        // The length of the buffer, rather than _textLength which is the same, lets the JIT remove the bounds check
        if ((uint)offset < (uint)buffer.Length)
        {
            _offset = offset;
            Current = buffer[offset];
        }
        else
        {
            AdvanceToEnd();
        }
    }

    // Moving to the end of the text, or staying there, which records that the end of the buffer was reached
    private void AdvanceToEnd()
    {
        _offset = _textLength;
        Eof = true;
        _flags |= HitEndFlag;
        Current = NullChar;
    }

    /// <summary>
    /// Moves the cursor to an absolute offset, usually a value of <see cref="Offset"/> read before the cursor advanced.
    /// </summary>
    /// <param name="offset">The absolute offset to move to.</param>
    /// <remarks>
    /// This is the way for a parser to move the cursor back when it doesn't match: saving <see cref="Offset"/> costs nothing
    /// whereas reading <see cref="Position"/> computes the line and the column.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetPosition(int offset)
    {
        if (offset - _base != _offset)
        {
            ResetPositionNotInlined(offset);
        }
    }

    /// <summary>
    /// Moves the cursor to the offset of a position.
    /// </summary>
    /// <param name="position">The position to move to.</param>
    /// <remarks>
    /// Only <see cref="TextPosition.Offset"/> is used: the line and the column of the cursor are always computed from the
    /// text, so a position whose line or column don't match its offset doesn't change them. Prefer saving <see cref="Offset"/>
    /// and calling <see cref="ResetPosition(int)"/>, which doesn't compute the line and the column.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetPosition(in TextPosition position) => ResetPosition(position.Offset);

    private void ResetPositionNotInlined(int offset)
    {
        // A parser moves the cursor back when it fails after reading: the position it read up to is a candidate for the
        // furthest failure. Only this path, which runs when the cursor actually moves, pays the comparison.
        var from = _offset + _base;
        var furthest = _furthestOffset;
        _furthestOffset = from > furthest ? from : furthest;

        _offset = offset - _base;

        // A single unsigned compare covers both the end of the buffer and, in compacting mode, a discarded position.
        // Unchecked since the offset is negative for a discarded position, and generated parsers embed this file in
        // assemblies which can check for arithmetic overflows.
        if (unchecked((uint)_offset) < (uint)Buffer.Length)
        {
            Current = Buffer[_offset];
            Eof = false;
        }
        else
        {
            ResetPositionOutsideBuffer();
        }
    }

    private void ResetPositionOutsideBuffer()
    {
        Current = NullChar;

        if (_offset < 0)
        {
            // The text was discarded by a compacting stream: nothing can be read until the cursor moves forward again.
            Eof = false;
        }
        else
        {
            // Eof might have been recorded
            Eof = true;
            _flags |= HitEndFlag;
        }
    }

    /// <summary>
    /// The furthest absolute offset the parse reached: the furthest offset the cursor was moved back from, or recorded with
    /// <see cref="RecordFailure(int)"/>, or the current offset when it is further.
    /// </summary>
    /// <remarks>
    /// Parsers move the cursor back when they fail after reading, so when a parse fails this is the classic "furthest
    /// failure" position: where the longest attempt to match the input stopped. Lookaheads which read past the match and
    /// move back count too.
    /// </remarks>
    internal int FurthestOffset
    {
        get
        {
            var offset = _offset + _base;
            return offset > _furthestOffset ? offset : _furthestOffset;
        }
    }

    /// <summary>
    /// Records an absolute offset a parser read up to without moving the cursor there, before it failed.
    /// </summary>
    /// <remarks>Generated span code reads the text through a local position and calls it on its failure path.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RecordFailure(int offset)
    {
        if (offset > _furthestOffset)
        {
            _furthestOffset = offset;
        }
    }

    /// <summary>
    /// Evaluates the char at the current position.
    /// </summary>
    public char Current { get; private set; }

    /// <summary>
    /// Returns the cursor's absolute offset in the input.
    /// </summary>
    public int Offset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _offset + _base;
    }

    /// <summary>
    /// Evaluates a char forward in the _buffer.
    /// </summary>
    public char PeekNext(int index = 1)
    {
        var nextIndex = _offset + index;

        if (nextIndex >= _textLength || nextIndex < 0)
        {
            if (nextIndex >= _textLength)
            {
                _flags |= HitEndFlag;
            }

            return NullChar;
        }

        return Buffer[nextIndex];
    }

    /// <summary>
    /// Advances the cursor by <paramref name="consumedLength"/> characters, or to the end of the text.
    /// </summary>
    /// <param name="consumedLength">How many characters were consumed.</param>
    /// <param name="newLines">Ignored: lines and columns are computed from the text when <see cref="Position"/> is read.</param>
    /// <param name="trailingSegmentLength">Ignored: lines and columns are computed from the text when <see cref="Position"/> is read.</param>
    /// <remarks>Prefer <see cref="Advance(int)"/>, the line and column arguments are only kept for compatibility.</remarks>
    public void AdvanceBy(int consumedLength, int newLines, int trailingSegmentLength)
    {
        _offset += consumedLength;

        if (_offset >= Buffer.Length)
        {
            Eof = true;
            _flags |= HitEndFlag;
            _offset = Buffer.Length;
            Current = NullChar;
        }
        else
        {
            Eof = false;
            Current = Buffer[_offset];
        }
    }

    public bool Eof { get; private set; }

    /// <summary>
    /// The buffered text. When the input is streamed in compacting mode it only holds the text the parser can still read,
    /// so read it with <see cref="Span"/>, <see cref="GetSpan(int, int)"/> or <see cref="CreateSpan(int, int)"/>, which take absolute offsets,
    /// rather than by indexing it with an offset.
    /// </summary>
    public string Buffer { get; private set; }

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

        if (_textLength < _offset + s.Length)
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
        if (_textLength < _offset + s.Length)
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
            _flags |= HitEndFlag;
        }
    }
}
